using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartOrderSystem.Models
{
    [Table("product_ratings")]
    public class ProductRating
    {
        [Key]
        [Column("rating_id")]
        public int rating_id { get; set; }

        [Required]
        [Column("shoe_id")]
        public int shoe_id { get; set; }

        [Required]
        [Column("customer_id")]
        public int customer_id { get; set; }

        [Required]
        [Range(1, 5)]
        [Column("rating")]
        public int rating { get; set; }
    }
}