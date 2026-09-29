using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartOrderSystem.Models
{
    [Table("orders")] // Siguraduhin na tumutugma sa table name sa MySQL
    public class Order
    {
        [Key]
        [Column("order_id")]
        public int order_id { get; set; }

        [Column("customer_id")]
        public int customer_id { get; set; }

        // Ito ay nasa database mo (address_id)
        [Column("address_id")]
        public int? address_id { get; set; }

        [Column("total_amount")]
        public decimal total_amount { get; set; }

        [Column("status")]
        public string status { get; set; } = "Pending";

        [Column("payment_method")]
        public string? payment_method { get; set; }

        [Column("payment_status")]
        public string payment_status { get; set; } = "Unpaid";

        [Column("order_date")]
        public DateTime order_date { get; set; } = DateTime.Now;

        [Column("courier")]
        public string? courier { get; set; }

        [Column("tracking_number")]
        public string? tracking_number { get; set; }
        
        // DAHIL WALA ITO SA DATABASE, GAGAWIN NATIN ITONG [NotMapped]
        [NotMapped] 
        public string? shipping_address { get; set; }

        [Column("shipping_status")]
        public string? shipping_status { get; set; } = "Pending";

        [Column("cancellation_reason")]
        public string? cancellation_reason { get; set; }

        [Column("updated_at")]
        public DateTime? updated_at { get; set; }

        // Navigation
        [ForeignKey("customer_id")]
        public Customer? Customer { get; set; }

        [ForeignKey(nameof(address_id))]
        public CustomerAddress? ShippingAddress { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}
