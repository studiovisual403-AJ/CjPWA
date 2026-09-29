namespace SmartOrderSystem.Models
{
    public class ProductCardViewModel
    {
        public int ShoeId { get; set; }
        public string ModelName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal? DiscountPercentage { get; set; }
        public string? ImagePath { get; set; }
        public int TotalStock { get; set; }
        public double AvgRating { get; set; }
        public  int RatingCount { get; set; }
        public int SoldCount { get; set; }
    }
}