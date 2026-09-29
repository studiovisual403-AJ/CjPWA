using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartOrderSystem.Models
{
    [Table("reservations")] // Siguraduhin na ito ang table name sa database
    public class Reservation
    {
        [Key]
        [Column("reservation_id")]
        public int reservation_id { get; set; }

        [Column("status")]
        public string status { get; set; } = "Pending";

        [Column("reservation_date")]
        public DateTime reservation_date { get; set; }

        [Column("customer_id")]
        public int customer_id { get; set; }
        
        [ForeignKey(nameof(customer_id))]
        public Customer? Customer { get; set; }

        [Column("quantity")]
        public int quantity { get; set; }

        [Column("approved_at")]
        public DateTime? approved_at { get; set; }

        [Column("approved_by")]
        public int? approved_by { get; set; }

        [Column("converted_order_id")]
        public int? converted_order_id { get; set; }

        [Column("expires_at")]
        public DateTime? expires_at { get; set; }

        [Column("inventory_id")]
        public int inventory_id { get; set; }

        [ForeignKey(nameof(inventory_id))]
        public ShoeInventory? ShoeInventory { get; set; }
    }
}