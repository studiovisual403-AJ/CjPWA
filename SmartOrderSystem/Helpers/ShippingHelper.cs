namespace SmartOrderSystem.Helpers
{
    public static class ShippingHelper
    {
        public const string StoreCity = "REPLACE_ME";
        private const string Zone1City = StoreCity;
        private const string Zone2City1 = "REPLACE_ME_1";
        private const string Zone2City2 = "REPLACE_ME_2";

        private static readonly Dictionary<string, decimal> CityShippingFees = new(StringComparer.OrdinalIgnoreCase)
        {
            [Zone1City] = 80m,
            [Zone2City1] = 150m,
            [Zone2City2] = 150m
        };

        public static decimal CalculateShipping(string? city, bool isFirstOrder)
        {
            if (isFirstOrder)
                return 0m;

            var normalizedCity = city?.Trim();
            return !string.IsNullOrEmpty(normalizedCity) && CityShippingFees.TryGetValue(normalizedCity, out var shippingFee)
                ? shippingFee
                : 250m;
        }

        public static (decimal Subtotal, decimal Shipping, decimal Total) CalculateTotals(
            decimal subtotal,
            string? city,
            bool isFirstOrder)
        {
            var shipping = CalculateShipping(city, isFirstOrder);
            return (subtotal, shipping, subtotal + shipping);
        }
    }
}
