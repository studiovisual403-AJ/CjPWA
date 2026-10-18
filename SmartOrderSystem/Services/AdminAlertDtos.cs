namespace SmartOrderSystem.Services
{
    public class AdminAlertSummaryDto
    {
        public int PendingOrderCount { get; set; }
        public List<PendingOrderAlertDto> PendingOrders { get; set; } = new();
        public int PendingReservationCount { get; set; }
        public List<PendingReservationAlertDto> PendingReservations { get; set; } = new();
        public int CancellationCount { get; set; }
        public List<CancelledOrderAlertDto> RecentCancellations { get; set; } = new();
        public int RopAlertCount { get; set; }
        public List<RopAlertDto> RopAlerts { get; set; } = new();

        public int AttentionCount => PendingOrderCount + PendingReservationCount + RopAlertCount;
    }

    public class PendingOrderAlertDto
    {
        public int OrderId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public DateTime OrderDate { get; set; }
        public double AgeHours { get; set; }
        public bool IsOverdue { get; set; }
    }

    public class PendingReservationAlertDto
    {
        public int ReservationId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public DateTime ReservationDate { get; set; }
        public double AgeHours { get; set; }
        public bool IsOverdue { get; set; }
    }

    public class CancelledOrderAlertDto
    {
        public int OrderId { get; set; }
        public string? CancellationReason { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class RopAlertDto
    {
        public int InventoryId { get; set; }
        public string ModelName { get; set; } = string.Empty;
        public decimal Size { get; set; }
        public int Stock { get; set; }
        public int ReorderPoint { get; set; }
        public string Status { get; set; } = string.Empty;
        public double StockToRopRatio { get; set; }
    }
}
