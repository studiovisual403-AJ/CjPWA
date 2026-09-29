using System.ComponentModel.DataAnnotations;

using System.ComponentModel.DataAnnotations.Schema;

namespace SmartOrderSystem.Models

{

    [Table("shoes_catalog")]

        public class ShoeCatalog

    {

        [Key]

        [Column("shoe_id")]

        public int shoe_id { get; set; }

        [Column("image_path")]

        public string? image_path { get; set; }

        [Column("model_name")]

        public string model_name { get; set; } = string.Empty;

        [Column("brand")]

        public string brand { get; set; } = string.Empty;

        [Column("category")]

        public string category { get; set; } = string.Empty;
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("color")]

        public string? color { get; set; }

        [Column("default_price")]

        public decimal default_price { get; set; }

        [Column("status")]

        public string status { get; set; } = "Active";
        public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
        public ICollection<ProductPromotion> Promotions { get; set; } = new List<ProductPromotion>();
    }   
}
