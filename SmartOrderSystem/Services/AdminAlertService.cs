using Microsoft.EntityFrameworkCore;
using SmartOrderSystem.Data;

namespace SmartOrderSystem.Services
{
    public class AdminAlertService : IAdminAlertService
    {
        public const int OverdueHours = 24;
        private const int CancellationLookbackDays = 30;
        private const int TopItemLimit = 5;

        private readonly ApplicationDbContext _context;
        private readonly IReorderPointService _reorderPointService;

        public AdminAlertService(
            ApplicationDbContext context,
            IReorderPointService reorderPointService)
        {
            _context = context;
            _reorderPointService = reorderPointService;
        }

        public async Task<AdminAlertSummaryDto> GetSummaryAsync()
        {
            var now = DateTime.Now;
            var since = now.AddDays(-CancellationLookbackDays);

            var pendingOrderQuery = _context.Orders
                .AsNoTracking()
                .Where(o => AdminAlertStatuses.WaitingOrderStatuses.Contains(o.status));

            var pendingOrderCount = await pendingOrderQuery.CountAsync();
            var pendingOrderRows = await pendingOrderQuery
                .OrderBy(o => o.order_date)
                .ThenBy(o => o.order_id)
                .Take(TopItemLimit)
                .Select(o => new
                {
                    o.order_id,
                    CustomerName = o.Customer == null ? "Unknown" : o.Customer.full_name,
                    o.total_amount,
                    o.order_date
                })
                .ToListAsync();

            var pendingOrders = pendingOrderRows
                .Select(o =>
                {
                    var ageHours = Math.Max(0, (now - o.order_date).TotalHours);
                    return new PendingOrderAlertDto
                    {
                        OrderId = o.order_id,
                        CustomerName = o.CustomerName,
                        Total = o.total_amount,
                        OrderDate = o.order_date,
                        AgeHours = ageHours,
                        IsOverdue = ageHours > OverdueHours
                    };
                })
                .ToList();

            var pendingReservationQuery = _context.Reservations
                .AsNoTracking()
                .Where(r => r.status == AdminAlertStatuses.Pending &&
                            (!r.expires_at.HasValue || r.expires_at.Value > now));

            var pendingReservationCount = await pendingReservationQuery.CountAsync();
            var pendingReservationRows = await pendingReservationQuery
                .OrderBy(r => r.reservation_date)
                .ThenBy(r => r.reservation_id)
                .Take(TopItemLimit)
                .Select(r => new
                {
                    r.reservation_id,
                    CustomerName = r.Customer == null ? "Unknown" : r.Customer.full_name,
                    r.quantity,
                    r.reservation_date
                })
                .ToListAsync();

            var pendingReservations = pendingReservationRows
                .Select(r =>
                {
                    var ageHours = Math.Max(0, (now - r.reservation_date).TotalHours);
                    return new PendingReservationAlertDto
                    {
                        ReservationId = r.reservation_id,
                        CustomerName = r.CustomerName,
                        Quantity = r.quantity,
                        ReservationDate = r.reservation_date,
                        AgeHours = ageHours,
                        IsOverdue = ageHours > OverdueHours
                    };
                })
                .ToList();

            var cancelledOrderQuery = _context.Orders
                .AsNoTracking()
                .Where(o => o.status == AdminAlertStatuses.Cancelled &&
                            o.updated_at.HasValue &&
                            o.updated_at.Value >= since &&
                            o.updated_at.Value <= now);

            var cancellationCount = await cancelledOrderQuery.CountAsync();
            var recentCancellations = await cancelledOrderQuery
                .OrderByDescending(o => o.updated_at)
                .ThenByDescending(o => o.order_id)
                .Take(TopItemLimit)
                .Select(o => new CancelledOrderAlertDto
                {
                    OrderId = o.order_id,
                    CancellationReason = o.cancellation_reason,
                    UpdatedAt = o.updated_at!.Value
                })
                .ToListAsync();

            var reorderRows = await _reorderPointService.GetRowsAsync();
            var ropAlerts = reorderRows
                .Where(r => r.Status == AdminAlertStatuses.OutOfStock ||
                            r.Status == AdminAlertStatuses.ReorderNow)
                .Select(r => new RopAlertDto
                {
                    InventoryId = r.InventoryId,
                    ModelName = r.ModelName,
                    Size = r.Size,
                    Stock = r.Stock,
                    ReorderPoint = r.ReorderPoint,
                    Status = r.Status,
                    StockToRopRatio = r.ReorderPoint <= 0
                        ? 0
                        : (double)r.Stock / r.ReorderPoint
                })
                .OrderBy(r => r.Status == AdminAlertStatuses.OutOfStock ? 0 : 1)
                .ThenBy(r => r.StockToRopRatio)
                .ThenBy(r => r.InventoryId)
                .ToList();

            return new AdminAlertSummaryDto
            {
                PendingOrderCount = pendingOrderCount,
                PendingOrders = pendingOrders,
                PendingReservationCount = pendingReservationCount,
                PendingReservations = pendingReservations,
                CancellationCount = cancellationCount,
                RecentCancellations = recentCancellations,
                RopAlertCount = ropAlerts.Count,
                RopAlerts = ropAlerts.Take(TopItemLimit).ToList()
            };
        }
    }
}
