namespace SmartOrderSystem.Models
{
    public class CustomerReservationViewModel
    {
        public int ReservationId { get; set; }
        public string ShoeName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
        public decimal Size { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime ReservationDate { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? ApprovedAt { get; set; }

        public string ReservationCode => $"RSV-{ReservationDate:yyyy}-{ReservationId:D4}";
        public decimal TotalPrice => Price * Quantity;
    }

    public class ReservationListViewModel
    {
        public List<CustomerReservationViewModel> ActiveReservations { get; set; } = new();
        public List<CustomerReservationViewModel> HistoryReservations { get; set; } = new();

        public int AllCount { get; set; }
        public int ActiveCount { get; set; }
        public int CompletedCount { get; set; }
        public int ExpiredCount { get; set; }
        public int CancelledCount { get; set; }

        public string CurrentTab { get; set; } = "All";
        public string CurrentSort { get; set; } = "recent";
    }
}