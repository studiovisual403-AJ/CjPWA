using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartOrderSystem.Models
{
    [Table("product_images")]
    public class ProductImage
    {
        [Key]
        [Column("id")] // I-map sa tamang column name sa MySQL
        public int Id { get; set; }

        [Column("file_name")]
        public string FileName { get; set; }

        [Column("file_path")]
        public string FilePath { get; set; }

        [Column("file_size")]
        public long FileSize { get; set; }

        [Column("is_cover")]
        public bool IsCover { get; set; }

        [Column("display_order")] // Dito ang fix!
        public int DisplayOrder { get; set; }
        
        [Column("shoe_catalog_id")]
        public int ShoeCatalogId { get; set; }
        
        [ForeignKey("ShoeCatalogId")]
        public ShoeCatalog ShoeCatalog { get; set; }
    }
}