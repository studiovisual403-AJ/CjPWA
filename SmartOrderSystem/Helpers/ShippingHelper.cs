namespace SmartOrderSystem.Helpers
{
    public static class ShippingHelper
    {
        public const decimal ShippingFee = 150m;
        public const decimal FreeShippingThreshold = 2000m;

        public static decimal CalculateShipping(decimal subtotal)
            => subtotal >= FreeShippingThreshold ? 0m : ShippingFee;

        public static (decimal ShippingFee, decimal Total) CalculateTotals(decimal discountedSubtotal)
        {
            var shippingFee = CalculateShipping(discountedSubtotal);
            return (shippingFee, discountedSubtotal + shippingFee);
        }
    }
}
