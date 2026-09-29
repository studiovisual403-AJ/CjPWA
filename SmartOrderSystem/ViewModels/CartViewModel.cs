namespace SmartOrderSystem.Models.ViewModels
{
    public class CartViewModel
    {
        public List<CartItemDisplay> Items { get; set; } = new();
        public decimal Subtotal { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal Total => Subtotal + ShippingFee;
        public decimal FreeShippingThreshold { get; set; } = 2000m;
        public decimal AmountAwayFromFreeShipping => Math.Max(0, FreeShippingThreshold - Subtotal);
        public double FreeShippingProgressPercent => (double)Math.Min(100, (Subtotal / FreeShippingThreshold) * 100);
        public bool QualifiesForFreeShipping => Subtotal >= FreeShippingThreshold;
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