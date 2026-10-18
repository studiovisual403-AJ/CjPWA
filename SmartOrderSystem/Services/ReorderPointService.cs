using System.Data;
using Microsoft.EntityFrameworkCore;
using SmartOrderSystem.Data;
using SmartOrderSystem.ViewModels;

namespace SmartOrderSystem.Services
{
    public class ReorderPointService : IReorderPointService
    {
        public const int RopLookbackDays = 90;
        public const int RopLeadTimeDays = 7;
        public const double RopServiceZ = 1.65;

        private readonly ApplicationDbContext _context;

        public ReorderPointService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<RopRowViewModel>> GetRowsAsync()
        {
            // RepeatableRead = consistent snapshot: the stock and sales data
            // used for the computation come from the same database moment.
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);

            var since = DateTime.Today.AddDays(-RopLookbackDays);

            var inventory = await _context.ShoeInventories
                .AsNoTracking()
                .Include(i => i.ShoeCatalog)
                .ToListAsync();

            var sales = await _context.Orders
                .AsNoTracking()
                .Where(o => o.order_date >= since &&
                            o.order_date <= DateTime.Now &&
                            o.status != AdminAlertStatuses.Pending &&
                            o.status != AdminAlertStatuses.Cancelled)
                .SelectMany(o => o.OrderItems.Select(i => new { i.inventory_id, i.quantity, o.order_date }))
                .ToListAsync();

            await transaction.CommitAsync();

            var dailyByInventory = sales
                .GroupBy(s => s.inventory_id)
                .ToDictionary(
                    g => g.Key,
                    g => g.GroupBy(x => x.order_date.Date).Select(d => d.Sum(x => x.quantity)).ToList());

            var rows = new List<RopRowViewModel>();

            foreach (var inv in inventory)
            {
                dailyByInventory.TryGetValue(inv.inventory_id, out var dailyTotals);
                dailyTotals ??= new List<int>();

                double totalSold = dailyTotals.Sum();
                double avgDaily = totalSold / RopLookbackDays;

                // Include days with no sales (demand = 0) in the variance.
                double sumSquares = dailyTotals.Sum(q => Math.Pow(q - avgDaily, 2));
                int zeroDays = RopLookbackDays - dailyTotals.Count;
                sumSquares += zeroDays * Math.Pow(avgDaily, 2);
                double stdDev = Math.Sqrt(sumSquares / RopLookbackDays);

                double safetyStock = RopServiceZ * stdDev * Math.Sqrt(RopLeadTimeDays);
                int reorderPoint = (int)Math.Ceiling(avgDaily * RopLeadTimeDays + safetyStock);

                string rowStatus;
                if (inv.quantity_in_stock <= 0) rowStatus = AdminAlertStatuses.OutOfStock;
                else if (totalSold == 0) rowStatus = AdminAlertStatuses.NoDemandData;
                else if (inv.quantity_in_stock <= reorderPoint) rowStatus = AdminAlertStatuses.ReorderNow;
                else rowStatus = AdminAlertStatuses.Healthy;

                rows.Add(new RopRowViewModel
                {
                    InventoryId = inv.inventory_id,
                    ImageUrl = inv.ShoeCatalog?.image_path,
                    Brand = inv.ShoeCatalog?.brand ?? "Unknown",
                    ModelName = inv.ShoeCatalog?.model_name ?? "Unknown",
                    Size = inv.size,
                    Stock = inv.quantity_in_stock,
                    AvgDailyDemand = Math.Round(avgDaily, 2),
                    SafetyStock = (int)Math.Ceiling(safetyStock),
                    ReorderPoint = reorderPoint,
                    Status = rowStatus
                });
            }

            return rows;
        }
    }
}
