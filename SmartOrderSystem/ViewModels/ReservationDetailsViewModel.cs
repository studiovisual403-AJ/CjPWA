using SmartOrderSystem.ViewModels;

namespace SmartOrderSystem.ViewModels
{
    public class ReservationDetailsViewModel
    {
        public int ReservationId { get; set; }
        
        // Customer Details
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public string CustomerPhone { get; set; }
        
        // Shoe Details
        public string ImagePath { get; set; }
        public string ShoeName { get; set; }
        public string Brand { get; set; }
        public string Category { get; set; }
        public string Color { get; set; }
        public decimal Price { get; set; }
        
        // Reservation Info
        public int Quantity { get; set; }
        public DateTime ReservationDate { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string Status { get; set; }
    }
}