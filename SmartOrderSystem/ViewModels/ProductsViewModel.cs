namespace SmartOrderSystem.Models
{
    public class ProductsViewModel
    {
        public List<ProductCardViewModel> Products { get; set; } = new();
        public string? SelectedCategory { get; set; }
        public string? SearchString { get; set; }
        public string? Sort { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
        public int PageSize { get; set; }
    }
}