namespace SmartOrderSystem.Models.ViewModels
{
    public class CartViewModel
    {
        public List<CartItemDisplay> Items { get; set; } = new();
        public decimal Subtotal { get; set; }
    }

    public class CartItemDisplay
    {
        public int CartItemId { get; set; }
        public int ShoeId { get; set; }
        public string Name { get; set; }
        public string Variant { get; set; }
        public string Size { get; set; }
        public string Color { get; set; }
        public string ImageUrl { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal LineTotal => Price * Quantity;
    }
}