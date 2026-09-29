using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOrderSystem.ViewModels;
using SmartOrderSystem.Models;
using SmartOrderSystem.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SmartOrderSystem.Controllers
{
    public class InventoryController : AdminBaseController
    {
        private readonly ApplicationDbContext _context;

        public InventoryController(ApplicationDbContext context) : base(context)
        {
            _context = context;
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

        // 2. LIVE SAVING TO DATABASE ONCE RESTOCK IS CLICKED
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restock(string Sku, decimal Size, int Quantity)
        {
            if (string.IsNullOrWhiteSpace(Sku) || Quantity <= 0 || Size < 36 || Size > 45 || Size != decimal.Truncate(Size))
            {
                TempData["Error"] = "Please enter a valid quantity.";
                return RedirectToAction("Index");
            }

            if (!Sku.StartsWith("SHOE-", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "The product SKU format is invalid.";
                return RedirectToAction("Index");
            }

            string idPart = Sku[5..];
            if (!int.TryParse(idPart, out int targetShoeId))
            {
                TempData["Error"] = "The product SKU format is invalid.";
                return RedirectToAction("Index");
            }

            // FIX 2: Direktang gamitin ang `Size` (dahil decimal na ito sa parameter) o i-convert nang tama kung kinakailangan
            decimal sizeDecimal = Size; 

            if (!await _context.ShoeCatalogs.AnyAsync(s => s.shoe_id == targetShoeId))
            {
                TempData["Error"] = "The selected product does not exist.";
                return RedirectToAction("Index");
            }

            var inventoryRecord = await _context.ShoeInventories
                .FirstOrDefaultAsync(x => x.shoe_id == targetShoeId && x.size == sizeDecimal);

            if (inventoryRecord != null)
            {
                if (inventoryRecord.quantity_in_stock > int.MaxValue - Quantity)
                {
                    TempData["Error"] = "The resulting stock quantity is too large.";
                    return RedirectToAction("Index");
                }
                inventoryRecord.quantity_in_stock += Quantity;
                _context.ShoeInventories.Update(inventoryRecord);
            }
            else
            {
                var newInventory = new ShoeInventory
                {
                    shoe_id = targetShoeId,
                    size = sizeDecimal, // FIX 3: Ginamit ang `sizeDecimal` sa halip na `sizeStr`
                    quantity_in_stock = Quantity,
                };
                await _context.ShoeInventories.AddAsync(newInventory);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Stock updated successfully.";

            return RedirectToAction("Index");
        }
    }
}
