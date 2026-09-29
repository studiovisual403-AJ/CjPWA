using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartOrderSystem.Models
{
    [Table("order_items")]
    public class OrderItem
    {
        [Key]
        [Column("order_item_id")]
        public int order_item_id { get; set; }

        [ForeignKey("Order")]
        [Column("order_id")]
        public int order_id { get; set; }

        [ForeignKey(nameof(ShoeInventory))]
        [Column("inventory_id")]
        public int inventory_id { get; set; }

        [Column("quantity")]
        public int quantity { get; set; }

        [Column("item_price")]
        public decimal item_price { get; set; }

        public Order? Order { get; set; }

        public ShoeInventory? ShoeInventory { get ; set; }
    }
}