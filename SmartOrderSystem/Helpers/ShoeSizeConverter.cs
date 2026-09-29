namespace SmartOrderSystem.Helpers
{
    public static class ShoeSizeConverter
    {
        // CJ Shoes size guide (PH sizing -> foot length cm)
        private static readonly Dictionary<decimal, decimal> SizeToCm = new()
        {
            { 36m, 22.5m },
            { 37m, 23.0m },
            { 38m, 23.5m },
            { 39m, 24.0m },
            { 40m, 24.5m },
            { 41m, 25.0m },
            { 42m, 25.5m },
            { 43m, 26.0m },
            { 44m, 26.5m },
            { 45m, 27.0m }
        };

        public static decimal? ToCm(decimal size)
        {
            if (SizeToCm.TryGetValue(size, out var cm))
                return cm;

            // Fallback: kung walang exact match (e.g. 36.5), i-interpolate
            var lower = SizeToCm.Keys.Where(k => k < size).DefaultIfEmpty().Max();
            var upper = SizeToCm.Keys.Where(k => k > size).DefaultIfEmpty().Min();

            if (lower != default && upper != default)
            {
                var lowerCm = SizeToCm[lower];
                var upperCm = SizeToCm[upper];
                var ratio = (size - lower) / (upper - lower);
                return lowerCm + (upperCm - lowerCm) * ratio;
            }

            return null;
        }
    }
}