using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SmartOrderSystem.Data;

namespace SmartOrderSystem.Controllers
{
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true, Duration = 0)]
    public class DashboardController : AdminBaseController
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
           // --- RECENT ORDERS ---
    ViewBag.RecentOrders = await _context.Orders
        .Include(o => o.Customer) 
        .OrderByDescending(o => o.order_id)
        .Take(3)
        .Select(o => new 
        {
            OrderId = "#ORD-" + o.order_id.ToString("D4"),
            CustomerName = o.Customer != null ? o.Customer.full_name : "Walk-in Customer",
            Amount = o.total_amount,
            Status = o.status
        })
        .ToListAsync();

    var rawCriticalStocks = await _context.ShoeInventories
    .Include(s => s.ShoeCatalog)
    .Where(s => s.quantity_in_stock <= 4)
    .ToListAsync();

ViewBag.CriticalStock = rawCriticalStocks
    .GroupBy(s => s.shoe_id)
    .Select(g =>
    {
        var firstItem = g.First();
        var lowest = g.OrderBy(x => x.quantity_in_stock).FirstOrDefault();

        return new
        {
            ProductId = firstItem.shoe_id,
            ProductName = firstItem.ShoeCatalog?.model_name ?? "Unknown Shoe",
            Brand = firstItem.ShoeCatalog?.brand ?? "Unknown Brand",
            Color = firstItem.ShoeCatalog?.color ?? "Standard",
            
            // DITO NAKALAGAY ANG IMAGEURL
            // Siguraduhin na ang 'image_url' ay tumutugma sa property name sa iyong ShoeCatalog model
            ImageUrl = firstItem.ShoeCatalog?.image_path ?? "/images/default-shoe.png", 
            
            CriticalCount = g.Count(),
            LowestSize = lowest?.size,
            LowestQuantity = lowest?.quantity_in_stock ?? 0,
            BadSizes = g.Select(z => new { Size = z.size, Quantity = z.quantity_in_stock }).ToList()
        };
    })
    .Take(5)
    .ToList();

    // --- METRICS ---
    ViewBag.OutOfStockCount = await _context.ShoeInventories.CountAsync(s => s.quantity_in_stock == 0);
    ViewBag.ActiveReservations = await _context.Reservations
        .CountAsync(r => (r.status == "Pending" || r.status == "Approved")
                         && (!r.expires_at.HasValue || r.expires_at > DateTime.Now));
    ViewBag.TotalProducts = await _context.ShoeCatalogs.CountAsync();
    ViewBag.UrgentAlerts = ViewBag.OutOfStockCount;
    var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
    var nextMonthStart = monthStart.AddMonths(1);
    ViewBag.TotalMonthlySales = await _context.OrderItems
        .Where(i => i.Order!.status == "Completed" &&
                    i.Order.order_date >= monthStart &&
                    i.Order.order_date < nextMonthStart)
        .SumAsync(i => (decimal?)(i.quantity * i.item_price)) ?? 0;
    
    ViewBag.SalesMonth = DateTime.Now.ToString("MMMM yyyy");

    return View();
        }
    }
}
