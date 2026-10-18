using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SmartOrderSystem.ViewModels;
using SmartOrderSystem.Models;
using SmartOrderSystem.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data;
using SmartOrderSystem.Helpers;
using SmartOrderSystem.Services;

namespace SmartOrderSystem.Controllers
{
    public class InventoryController : AdminBaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IReorderPointService _reorderPointService;

        public InventoryController(ApplicationDbContext context, IReorderPointService reorderPointService) : base(context)
        {
            _context = context;
            _reorderPointService = reorderPointService;
        }

        private static List<decimal> GetTargetSizes()
        {
            return new List<decimal> { 36, 37, 38, 39, 40, 41, 42, 43, 44, 45 };
        }

        // 1. DISPLAY THE LIVE DYNAMIC INVENTORY WITH PAGINATION
        public async Task<IActionResult> Index(int page = 1)
        {
            page = Math.Max(1, page);
            int pageSize = 10; // 10 rows ng sapatos lang kada pahina
            int totalRecords = await _context.ShoeCatalogs.CountAsync();
            int totalPages = Math.Max(1, (int)Math.Ceiling((double)totalRecords / pageSize));
            page = Math.Min(page, totalPages);

            // B. DATABASE PAGINATION: Kumuha LANG ng 10 produkto na para sa page na ito
            var paginatedCatalog = await _context.ShoeCatalogs
                .OrderBy(c => c.shoe_id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // C. Kunin lang ang mga inventory rows na KABILANG sa 10 produktong nakuha natin sa itaas
            var targetShoeIds = paginatedCatalog.Select(c => c.shoe_id).ToList();
            var inventoryItems = await _context.ShoeInventories
                .Where(x => targetShoeIds.Contains(x.shoe_id))
                .ToListAsync();
            var stockByShoeAndSize = inventoryItems
                .GroupBy(x => new { x.shoe_id, x.size })
                .ToDictionary(g => (g.Key.shoe_id, g.Key.size), g => g.Sum(x => x.quantity_in_stock));

            var paginatedRows = new List<InventoryMatrixRow>();
            var targetSizes = GetTargetSizes();

            // D. Buuin ang Matrix para sa 10 produktong ito lang
            foreach (var item in paginatedCatalog)
            {
                var row = new InventoryMatrixRow
                {
                    ShoeId = item.shoe_id,
                    Brand = item.brand,
                    ModelName = $"{item.model_name} ({item.color})", 
                    Sku = $"SHOE-{item.shoe_id:D4}"
                };

                var itemStocks = inventoryItems.Where(x => x.shoe_id == item.shoe_id).ToList();

                foreach (var size in targetSizes)
                {
                    // FIX 1: Pinalitan ang "G29" ng kasalukuyang `size` mula sa loop
                    stockByShoeAndSize.TryGetValue((item.shoe_id, size), out var quantity);
                    row.StockBySize[size] = quantity;
                }

                paginatedRows.Add(row);
            }

            // E. Para sa Dashboard Summary Cards
            int totalItems = await _context.ShoeInventories.SumAsync(x => x.quantity_in_stock);
            int lowStockCount = await _context.ShoeInventories.CountAsync(x => x.quantity_in_stock > 0 && x.quantity_in_stock <= 5);
            int outOfStockCount = await _context.ShoeInventories.CountAsync(x => x.quantity_in_stock == 0);

            var viewModel = new InventoryDashboardViewModel
            {
                TotalItems = totalItems,
                LowStockCount = lowStockCount,
                OutOfStockCount = outOfStockCount,
                TotalProducts = totalRecords,
                SizeColumns = targetSizes,
                MatrixRows = paginatedRows,
                CurrentPage = page,
                TotalPages = totalPages
            };

            return View(viewModel);
        }

        // 2. RESTOCK: may row lock para hindi magkamali ang bilang kapag sabay ang restock/order
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Restock(string Sku, decimal Size, int Quantity)
{
    if (string.IsNullOrWhiteSpace(Sku) || Quantity <= 0 || Size < 36 || Size > 45 || Size != decimal.Truncate(Size))
    {
        TempData["Error"] = "Please enter a valid quantity.";
        return RedirectToAction("Index");
    }

    if (!Sku.StartsWith("SHOE-", StringComparison.OrdinalIgnoreCase) || !int.TryParse(Sku[5..], out int targetShoeId))
    {
        TempData["Error"] = "The product SKU format is invalid.";
        return RedirectToAction("Index");
    }

    decimal sizeDecimal = Size;

    if (!await _context.ShoeCatalogs.AnyAsync(s => s.shoe_id == targetShoeId))
    {
        TempData["Error"] = "The selected product does not exist.";
        return RedirectToAction("Index");
    }

    await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
    ShoeInventory? newInventoryRecord = null;

    try
    {
        var inventoryRecord = await _context.LockInventoryBySizeAsync(targetShoeId, sizeDecimal);

        if (inventoryRecord != null)
        {
            if (inventoryRecord.quantity_in_stock > int.MaxValue - Quantity)
            {
                TempData["Error"] = "The resulting stock quantity is too large.";
                return RedirectToAction("Index");
            }
            inventoryRecord.quantity_in_stock += Quantity;
        }
        else
        {
            newInventoryRecord = new ShoeInventory
            {
                shoe_id = targetShoeId,
                size = sizeDecimal,
                quantity_in_stock = Quantity
            };
            await _context.ShoeInventories.AddAsync(newInventoryRecord);
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Success"] = "Stock updated successfully.";
    }
    catch (DbUpdateException ex) when (
        newInventoryRecord != null &&
        ex.GetBaseException() is MySqlException { Number: 1062 })
    {
        await transaction.RollbackAsync();
        _context.Entry(newInventoryRecord).State = EntityState.Detached;

        await using var retryTransaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var inventoryRecord = await _context.LockInventoryBySizeAsync(targetShoeId, sizeDecimal);
        if (inventoryRecord == null)
        {
            await retryTransaction.RollbackAsync();
            TempData["Error"] = "Another update was in progress for this item. Please try again.";
            return RedirectToAction("Index");
        }

        if (inventoryRecord.quantity_in_stock > int.MaxValue - Quantity)
        {
            await retryTransaction.RollbackAsync();
            TempData["Error"] = "The resulting stock quantity is too large.";
            return RedirectToAction("Index");
        }

        inventoryRecord.quantity_in_stock += Quantity;
        await _context.SaveChangesAsync();
        await retryTransaction.CommitAsync();
        TempData["Success"] = "Stock updated successfully.";
    }
    catch (DbUpdateException)
    {
        await transaction.RollbackAsync();
        TempData["Error"] = "Another update was in progress for this item. Please try again.";
    }

    return RedirectToAction("Index");
}

        public async Task<IActionResult> ReorderPoints(string? status, string? q, int page = 1)
        {
            var rows = await _reorderPointService.GetRowsAsync();
            var normalizedStatus = string.IsNullOrWhiteSpace(status) ? "All" : status.Trim();
            var normalizedSearch = q?.Trim();
            var filtered = FilterAndSortRopRows(rows, normalizedStatus, normalizedSearch);

            int pageSize = 15;
            int totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);

            ViewBag.SearchTerm = normalizedSearch ?? string.Empty;
            ViewBag.TotalFiltered = filtered.Count;
            ViewBag.PageSize = pageSize;

            var vm = new RopDashboardViewModel
            {
                Rows = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                CurrentStatus = normalizedStatus,
                CurrentPage = page,
                TotalPages = totalPages,
                OutOfStockCount = rows.Count(r => r.Status == AdminAlertStatuses.OutOfStock),
                ReorderNowCount = rows.Count(r => r.Status == AdminAlertStatuses.ReorderNow),
                HealthyCount = rows.Count(r => r.Status == AdminAlertStatuses.Healthy),
                NoDataCount = rows.Count(r => r.Status == AdminAlertStatuses.NoDemandData),
                LookbackDays = ReorderPointService.RopLookbackDays,
                LeadTimeDays = ReorderPointService.RopLeadTimeDays,
                ServiceZ = ReorderPointService.RopServiceZ
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ExportReorderPoints(string? status, string? q)
        {
            var rows = await _reorderPointService.GetRowsAsync();
            var normalizedStatus = string.IsNullOrWhiteSpace(status) ? "All" : status.Trim();
            var filtered = FilterAndSortRopRows(rows, normalizedStatus, q?.Trim());

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("inventory_id,model_name,brand,size,quantity_in_stock,avg_daily_demand,safety_stock,reorder_point,status");
            foreach (var row in filtered)
            {
                csv.Append(row.InventoryId).Append(',')
                    .Append(CsvField(row.ModelName)).Append(',')
                    .Append(CsvField(row.Brand)).Append(',')
                    .Append(row.Size.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                    .Append(row.Stock).Append(',')
                    .Append(row.AvgDailyDemand.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                    .Append(row.SafetyStock).Append(',')
                    .Append(row.ReorderPoint).Append(',')
                    .Append(CsvField(row.Status)).AppendLine();
            }

            return File(
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(csv.ToString()),
                "text/csv; charset=utf-8",
                "reorder-points.csv");
        }

        private static List<RopRowViewModel> FilterAndSortRopRows(
            IEnumerable<RopRowViewModel> rows,
            string status,
            string? search)
        {
            var filtered = rows.Where(r =>
                    status == "All" || r.Status == status)
                .Where(r => string.IsNullOrWhiteSpace(search) ||
                            r.ModelName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                            r.Brand.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                            r.Size.ToString("0.#", System.Globalization.CultureInfo.CurrentCulture)
                                .Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();

            int Priority(string value) => value switch
            {
                AdminAlertStatuses.OutOfStock => 0,
                AdminAlertStatuses.ReorderNow => 1,
                AdminAlertStatuses.Healthy => 2,
                _ => 3
            };

            return filtered.OrderBy(r => Priority(r.Status))
                .ThenBy(r => r.Stock)
                .ToList();
        }

        private static string CsvField(string? value)
        {
            var escaped = (value ?? string.Empty).Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }
    }
}
