namespace SmartOrderSystem.Models
{
    public class SizeOptionViewModel
    {
        public int InventoryId { get; set; }
        public decimal Size { get; set; }
        public int Stock { get; set; }
        public decimal? Cm { get; set;}
    }

    public class ProductDetailsViewModel
    {
        public int ShoeId { get; set; }
        public string ModelName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? Color { get; set; }
        public decimal Price { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal? DiscountPercentage { get; set; }

        public List<string> ImagePaths { get; set; } = new();
        public List<SizeOptionViewModel> Sizes { get; set; } = new();

        public double AvgRating { get; set; }
        public int RatingCount { get; set; }
    }
}