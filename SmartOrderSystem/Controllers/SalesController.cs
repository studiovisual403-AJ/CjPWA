using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOrderSystem.Data;
using SmartOrderSystem.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SmartOrderSystem.Controllers
{
    public class SalesController : AdminBaseController
    {
        private readonly ApplicationDbContext _context;

        public SalesController(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        private static double CalculateGrowth(decimal current, decimal previous)
        {
            return previous == 0 ? 0 : (double)((current - previous) / previous) * 100;
        }

        private static double CalculateGrowth(int current, int previous)
        {
            return previous == 0 ? 0 : ((double)(current - previous) / previous) * 100;
        }

        [HttpGet]
        public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, string? brand, string? category, string? status, int page = 1)
        {
            var today = DateTime.Today;
            var start = startDate ?? new DateTime(today.Year, today.Month, 1);
            var end = endDate ?? today;

            if (end.Date < start.Date)
                return BadRequest("The end date cannot be earlier than the start date.");

            var model = await BuildSalesReportAsync(start, end, brand, category, status, page);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> IndexData(DateTime? startDate, DateTime? endDate, string? brand, string? category, string? status, int page = 1)
        {
            var today = DateTime.Today;
            var start = startDate ?? new DateTime(today.Year, today.Month, 1);
            var end = endDate ?? today;

            if (end.Date < start.Date)
                return BadRequest("The end date cannot be earlier than the start date.");

            var model = await BuildSalesReportAsync(start, end, brand, category, status, page);
            return Json(model);
        }

        [HttpGet]
        public async Task<IActionResult> ExportPdf(DateTime? startDate, DateTime? endDate, string? brand, string? category, string? status)
        {
            var today = DateTime.Today;
            var start = startDate ?? new DateTime(today.Year, today.Month, 1);
            var end = endDate ?? today;
            if (end.Date < start.Date)
                return BadRequest("The end date cannot be earlier than the start date.");

            var report = await BuildSalesReportAsync(start, end, brand, category, status, 1);
            QuestPDF.Settings.License = LicenseType.Community;
            var document = Document.Create(container => container.Page(page =>
            {
                page.Margin(32);
                page.Header().Text("CJ Shoes Sales Report").FontSize(18).Bold();
                page.Content().Column(column =>
                {
                    column.Item().Text($"Period: {report.DateStart:MMM dd, yyyy} to {report.DateEnd:MMM dd, yyyy}");
                    column.Item().Text($"Product revenue: {report.TotalSales:C2}");
                    column.Item().Text($"Completed orders: {report.CompletedOrders}");
                    column.Item().Text($"Products sold: {report.TotalProductsSold}");
                    column.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                        });
                        table.Header(header =>
                        {
                            header.Cell().Text("Order").Bold();
                            header.Cell().Text("Customer").Bold();
                            header.Cell().Text("Product").Bold();
                            header.Cell().Text("Qty").Bold();
                            header.Cell().Text("Amount").Bold();
                        });
                        foreach (var transaction in report.Transactions)
                        {
                            table.Cell().Text(transaction.OrderCode);
                            table.Cell().Text(transaction.CustomerName);
                            table.Cell().Text(transaction.ProductName);
                            table.Cell().Text(transaction.Qty.ToString());
                            table.Cell().Text(transaction.Amount.ToString("C2"));
                        }
                    });
                });
                page.Footer().AlignCenter().Text(text => text.Span("CJ Shoes"));
            })).GeneratePdf();

            return File(document, "application/pdf", $"sales-report-{start:yyyyMMdd}-{end:yyyyMMdd}.pdf");
        }

        private async Task<SalesDetailsViewModel> BuildSalesReportAsync(
            DateTime startDate, DateTime endDate, string? brand, string? category, string? status, int page)
        {
            if (page < 1) page = 1;
            const int pageSize = 5;

            var start = startDate.Date;
            var endExclusive = endDate.Date.AddDays(1);

            brand = string.IsNullOrWhiteSpace(brand) || brand == "All Brands" ? null : brand;
            category = string.IsNullOrWhiteSpace(category) || category == "All Categories" ? null : category;
            status = string.IsNullOrWhiteSpace(status) || status == "All Status" ? null : status;

            IQueryable<Models.OrderItem> completedItemsQuery = _context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.ShoeInventory!).ThenInclude(si => si.ShoeCatalog)
                .Where(oi => oi.Order!.status == (status ?? "Completed")
                    && oi.Order.order_date >= start
                    && oi.Order.order_date < endExclusive);

            if (brand != null)
                completedItemsQuery = completedItemsQuery.Where(oi => oi.ShoeInventory!.ShoeCatalog!.brand == brand);
            if (category != null)
                completedItemsQuery = completedItemsQuery.Where(oi => oi.ShoeInventory!.ShoeCatalog!.category == category);

            var completedItems = await completedItemsQuery
                .Select(oi => new
                {
                    oi.order_id,
                    oi.quantity,
                    oi.item_price,
                    Brand = oi.ShoeInventory!.ShoeCatalog!.brand,
                    Category = oi.ShoeInventory.ShoeCatalog.category,
                    ProductName = oi.ShoeInventory.ShoeCatalog.model_name,
                    ImageUrl = oi.ShoeInventory.ShoeCatalog.image_path,
                    ShoeId = oi.ShoeInventory.ShoeCatalog.shoe_id
                })
                .ToListAsync();

            decimal totalSales = completedItems.Sum(x => x.quantity * x.item_price);
            int completedOrders = completedItems.Select(x => x.order_id).Distinct().Count();
            int totalProductsSold = completedItems.Sum(x => x.quantity);
            decimal averageOrderValue = completedOrders == 0 ? 0 : totalSales / completedOrders;

            var periodLength = endExclusive - start;
            var prevStart = start - periodLength;
            var prevEnd = start;

            IQueryable<Models.OrderItem> prevQuery = _context.OrderItems
                .Where(oi => oi.Order!.status == (status ?? "Completed")
                    && oi.Order.order_date >= prevStart
                    && oi.Order.order_date < prevEnd);
            if (brand != null)
                prevQuery = prevQuery.Where(oi => oi.ShoeInventory!.ShoeCatalog!.brand == brand);
            if (category != null)
                prevQuery = prevQuery.Where(oi => oi.ShoeInventory!.ShoeCatalog!.category == category);

            var prevItems = await prevQuery
                .Select(oi => new { oi.order_id, oi.quantity, oi.item_price })
                .ToListAsync();

            decimal prevSales = prevItems.Sum(x => x.quantity * x.item_price);
            int prevOrders = prevItems.Select(x => x.order_id).Distinct().Count();
            int prevUnits = prevItems.Sum(x => x.quantity);
            decimal prevAvg = prevOrders == 0 ? 0 : prevSales / prevOrders;

            string[] brandColors = { "#ef4444", "#3b82f6", "#22c55e", "#8b5cf6", "#f59e0b" };
            var brandRaw = completedItems
                .GroupBy(x => x.Brand)
                .Select(g => new { Brand = g.Key, Revenue = g.Sum(x => x.quantity * x.item_price) })
                .OrderByDescending(b => b.Revenue)
                .ToList();
            decimal totalBrandRevenue = brandRaw.Sum(b => b.Revenue);
            var brandSales = brandRaw.Select((b, i) => new BrandSalesItem
            {
                Brand = b.Brand,
                Revenue = b.Revenue,
                Percentage = totalBrandRevenue == 0 ? 0 : (double)(b.Revenue / totalBrandRevenue) * 100,
                Color = brandColors[i % brandColors.Length]
            }).ToList();

            var categorySales = completedItems
                .GroupBy(x => x.Category)
                .Select(g => new CategorySalesItem { Category = g.Key, Amount = g.Sum(x => x.quantity * x.item_price) })
                .OrderByDescending(c => c.Amount)
                .ToList();

            var bestSelling = completedItems
                .GroupBy(x => new { x.ShoeId, x.ProductName, x.Brand, x.Category, x.ImageUrl })
                .Select(g => new BestSellingProductItem
                {
                    ProductName = g.Key.ProductName,
                    Brand = g.Key.Brand,
                    Category = g.Key.Category,
                    ImageUrl = g.Key.ImageUrl,
                    UnitsSold = g.Sum(x => x.quantity),
                    Revenue = g.Sum(x => x.quantity * x.item_price)
                })
                .OrderByDescending(p => p.UnitsSold)
                .Take(5)
                .ToList();
            for (int i = 0; i < bestSelling.Count; i++) bestSelling[i].Rank = i + 1;

            var bestProduct = bestSelling.FirstOrDefault();

            IQueryable<Models.OrderItem> statusItemsQuery = _context.OrderItems
                .Where(oi => oi.Order!.order_date >= start && oi.Order.order_date < endExclusive);
            if (brand != null)
                statusItemsQuery = statusItemsQuery.Where(oi => oi.ShoeInventory!.ShoeCatalog!.brand == brand);
            if (category != null)
                statusItemsQuery = statusItemsQuery.Where(oi => oi.ShoeInventory!.ShoeCatalog!.category == category);
            if (status != null)
                statusItemsQuery = statusItemsQuery.Where(oi => oi.Order!.status == status);

            var statusRows = await statusItemsQuery
                .Select(oi => new { oi.order_id, oi.Order!.status })
                .ToListAsync();

            var distinctOrders = statusRows.GroupBy(x => x.order_id).Select(g => g.First()).ToList();
            int totalOrdersForStatus = distinctOrders.Count;

            var statusColors = new Dictionary<string, string>
            {
                { "Completed", "#22c55e" },
                { "Reserved", "#f59e0b" },
                { "Cancelled", "#ef4444" }
            };

            var orderStatusDistribution = distinctOrders
                .GroupBy(x => x.status)
                .Select(g => new OrderStatusItem
                {
                    Status = g.Key,
                    Count = g.Count(),
                    Percentage = totalOrdersForStatus == 0 ? 0 : (double)g.Count() / totalOrdersForStatus * 100,
                    Color = statusColors.TryGetValue(g.Key, out var c) ? c : "#94a3b8"
                })
                .OrderByDescending(s => s.Count)
                .ToList();

            var monthlyTrend = new List<MonthlyTrendItem>();
            var trendMonthAnchor = new DateTime(endDate.Year, endDate.Month, 1);
            for (int i = 5; i >= 0; i--)
            {
                var monthStart = trendMonthAnchor.AddMonths(-i);
                var monthEnd = monthStart.AddMonths(1);

                IQueryable<Models.OrderItem> monthQuery = _context.OrderItems
                    .Where(oi => oi.Order!.status == (status ?? "Completed")
                        && oi.Order.order_date >= monthStart && oi.Order.order_date < monthEnd);
                if (brand != null)
                    monthQuery = monthQuery.Where(oi => oi.ShoeInventory!.ShoeCatalog!.brand == brand);
                if (category != null)
                    monthQuery = monthQuery.Where(oi => oi.ShoeInventory!.ShoeCatalog!.category == category);

                decimal monthAmount = await monthQuery.SumAsync(oi => (decimal?)(oi.quantity * oi.item_price)) ?? 0;
                monthlyTrend.Add(new MonthlyTrendItem { Month = monthStart.ToString("MMM"), Amount = monthAmount });
            }

            IQueryable<Models.OrderItem> txQuery = _context.OrderItems
                .Include(oi => oi.Order!).ThenInclude(o => o.Customer)
                .Include(oi => oi.ShoeInventory!).ThenInclude(si => si.ShoeCatalog)
                .Where(oi => oi.Order!.order_date >= start && oi.Order.order_date < endExclusive);

            if (brand != null)
                txQuery = txQuery.Where(oi => oi.ShoeInventory!.ShoeCatalog!.brand == brand);
            if (category != null)
                txQuery = txQuery.Where(oi => oi.ShoeInventory!.ShoeCatalog!.category == category);
            if (status != null)
                txQuery = txQuery.Where(oi => oi.Order!.status == status);
            else
                txQuery = txQuery.Where(oi => oi.Order!.status == "Completed");

            int totalTransactions = await txQuery.CountAsync();

            var txRaw = await txQuery
                .OrderByDescending(oi => oi.Order!.order_date)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(oi => new
                {
                    oi.order_id,
                    oi.quantity,
                    oi.item_price,
                    OrderDate = oi.Order!.order_date,
                    OrderStatus = oi.Order.status,
                    PaymentMethod = oi.Order.payment_method,
                    CustomerName = oi.Order.Customer != null ? oi.Order.Customer.full_name : "Guest",
                    ProductName = oi.ShoeInventory!.ShoeCatalog!.model_name,
                    Category = oi.ShoeInventory.ShoeCatalog.category
                })
                .ToListAsync();

            var transactions = txRaw.Select(t => new SalesTransactionItem
            {
                OrderCode = $"ORD-{t.OrderDate.Year}-{t.order_id:D4}",
                CustomerName = t.CustomerName,
                ProductName = t.ProductName,
                Category = t.Category,
                Qty = t.quantity,
                Amount = t.quantity * t.item_price,
                Date = t.OrderDate,
                Status = t.OrderStatus,
                PaymentMethod = t.PaymentMethod
            }).ToList();

            var brandOptions = await _context.ShoeCatalogs.Select(s => s.brand).Distinct().OrderBy(b => b).ToListAsync();
            var categoryOptions = await _context.ShoeCatalogs.Select(s => s.category).Distinct().OrderBy(c => c).ToListAsync();

            return new SalesDetailsViewModel
            {
                DateStart = startDate,
                DateEnd = endDate,
                SelectedBrand = brand,
                SelectedCategory = category,
                SelectedStatus = status,
                BrandOptions = brandOptions,
                CategoryOptions = categoryOptions,

                TotalSales = totalSales,
                TotalSalesGrowth = Math.Round(CalculateGrowth(totalSales, prevSales), 0),
                CompletedOrders = completedOrders,
                CompletedOrdersGrowth = Math.Round(CalculateGrowth(completedOrders, prevOrders), 0),
                TotalProductsSold = totalProductsSold,
                TotalProductsSoldGrowth = Math.Round(CalculateGrowth(totalProductsSold, prevUnits), 0),
                AverageOrderValue = averageOrderValue,
                AverageOrderValueGrowth = Math.Round(CalculateGrowth(averageOrderValue, prevAvg), 0),
                BestSellingProductName = bestProduct?.ProductName ?? "N/A",
                BestSellingProductUnits = bestProduct?.UnitsSold ?? 0,

                MonthlySalesTrend = monthlyTrend,
                BrandSales = brandSales,
                CategorySales = categorySales,
                OrderStatusDistribution = orderStatusDistribution,

                BestSellingProducts = bestSelling,
                Transactions = transactions,

                CurrentPage = page,
                PageSize = pageSize,
                TotalTransactions = totalTransactions
            };
        }
    }
}
