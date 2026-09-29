using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartOrderSystem.Models
{
    [Table("cart_items")]
    public class CartItem
    {
        [Key]
        public int CartItemId { get; set; }

        [Required]
        public int CustomerId { get; set; } // FK to Customers.customer_id

        [Required]
        public int ShoeId { get; set; } // FK to shoes_catalog

        [Required, StringLength(20)]
        public string Size { get; set; }

        [Required, StringLength(50)]
        public string Color { get; set; }

        [Required]
        public int Quantity { get; set; } = 1;

        [Column(TypeName = "decimal(10,2)")]
        public decimal PriceAtAddTime { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("ShoeId")]
        public virtual ShoeCatalog Shoe { get; set; }

        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; }
    }
}