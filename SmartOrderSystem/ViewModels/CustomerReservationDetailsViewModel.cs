namespace SmartOrderSystem.Models
{
    public class CustomerReservationDetailsViewModel
    {
        public int ReservationId { get; set; }
        public string ReservationCode => $"RSV-{ReservationDate:yyyy}-{ReservationId:D4}";

        public string ShoeName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? Color { get; set; }
        public List<string> ImagePaths { get; set; } = new();

        public decimal Size { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal TotalPrice => Price * Quantity;

        public string Status { get; set; } = string.Empty;
        public DateTime ReservationDate { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public int? ConvertedOrderId { get; set; }
    }
}