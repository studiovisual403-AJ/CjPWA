using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartOrderSystem.Models
{
    public class Notification
    {
        [Key]
        public int notification_id { get; set; }

        // Nullable na ngayon - hindi lahat ng notification ay naka-attach sa isang order
        // (e.g. Promotion, NewArrival)
        public int? order_id { get; set; }

        public int? customer_id { get; set; }

        // Optional na reference para sa Promotion/NewArrival notifications
        public int? promotion_id { get; set; }
        public int? shoe_id { get; set; }

        public string message { get; set; } = string.Empty;
        public string title { get; set; } = string.Empty;

        // ReservationConfirmed, Confirmed, Preparing, InTransit, Delivered, Completed, Promotion, NewArrival
        public string type { get; set; } = string.Empty;

        public DateTime created_at { get; set; } = DateTime.Now;
        public bool is_read { get; set; } = false;

        [ForeignKey("order_id")]
        public Order? Order { get; set; }

        [ForeignKey("promotion_id")]
        public ProductPromotion? Promotion { get; set; }

        [ForeignKey("shoe_id")]
        public ShoeCatalog? Shoe { get; set; }
    }
}