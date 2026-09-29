using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartOrderSystem.Models
{
    [Table("shoe_inventory")]
    public class ShoeInventory
    {
        [Key]
        [Column("inventory_id")]
        public int inventory_id { get; set; }

        [Column("shoe_id")]
        [ForeignKey(nameof(ShoeCatalog))]
        public int shoe_id { get; set; }

        public decimal size { get; set; }

        public int quantity_in_stock { get; set; }

        public ShoeCatalog? ShoeCatalog { get; set; }
    }
}