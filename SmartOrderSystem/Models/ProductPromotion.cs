using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SmartOrderSystem.Models
{
    [Table("product_promotions")]
    public class ProductPromotion
    {
    [Key]
    public int promotion_id { get; set; }

    public int shoe_id { get; set; }
    public string campaign_name { get; set; }
    public decimal discount_percentage { get; set; }
    public DateTime start_date { get; set; }
    public DateTime end_date { get; set; }
    public string status { get; set; }

    [ForeignKey("shoe_id")]
    public ShoeCatalog? Shoe { get; set; }
    }
}