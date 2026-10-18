namespace SmartOrderSystem.Helpers;

public static class ReportAnalytics
{
    public const decimal AbcThresholdA = 0.80m;
    public const decimal AbcThresholdB = 0.95m;
    public const int SmaWindow = 3;

    public static AbcAnalysis AnalyzeAbc(IEnumerable<ProductRevenueInput> sales)
    {
        ArgumentNullException.ThrowIfNull(sales);

        var products = sales
            .GroupBy(sale => sale.ShoeId)
            .Select(group => new
            {
                ShoeId = group.Key,
                ProductName = group.Select(sale => sale.ProductName).OrderBy(name => name, StringComparer.Ordinal).First(),
                Quantity = group.Sum(sale => sale.Quantity),
                Revenue = group.Sum(sale => sale.Quantity * sale.UnitPrice)
            })
            .Where(product => product.Quantity > 0)
            .OrderByDescending(product => product.Revenue)
            .ThenBy(product => product.ProductName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(product => product.ShoeId)
            .ToList();

        var totalRevenue = products.Sum(product => product.Revenue);
        if (totalRevenue == 0m)
            return new AbcAnalysis(
                new List<AbcProductResult>(),
                new[] { "A", "B", "C" }
                    .Select(className => new AbcClassSummary(className, 0, 0m))
                    .ToList());

        var results = new List<AbcProductResult>(products.Count);
        decimal cumulativeRevenue = 0m;

        foreach (var product in products)
        {
            var shareBeforeProduct = cumulativeRevenue / totalRevenue;
            var classification = shareBeforeProduct < AbcThresholdA
                ? "A"
                : shareBeforeProduct < AbcThresholdB
                    ? "B"
                    : "C";
            var share = product.Revenue / totalRevenue;
            cumulativeRevenue += product.Revenue;

            results.Add(new AbcProductResult(
                product.ShoeId,
                product.ProductName,
                product.Revenue,
                share,
                cumulativeRevenue / totalRevenue,
                classification));
        }

        var classSummaries = results
            .GroupBy(product => product.Classification)
            .ToDictionary(
                group => group.Key,
                group => new AbcClassSummary(
                    group.Key,
                    group.Count(),
                    group.Sum(product => product.Revenue) / totalRevenue));

        return new AbcAnalysis(
            results,
            new[] { "A", "B", "C" }
                .Select(className => classSummaries.TryGetValue(className, out var summary)
                    ? summary
                    : new AbcClassSummary(className, 0, 0m))
                .ToList());
    }

    public static SmaAnalysis CalculateSma(IEnumerable<MonthlyRevenue> monthlyRevenue)
    {
        ArgumentNullException.ThrowIfNull(monthlyRevenue);

        var revenueByMonth = monthlyRevenue
            .GroupBy(item => new DateTime(item.Month.Year, item.Month.Month, 1))
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Revenue));

        if (revenueByMonth.Count == 0)
            return new SmaAnalysis(new List<MonthlySmaPoint>(), null);

        var firstMonth = revenueByMonth.Keys.Min();
        var lastMonth = revenueByMonth.Keys.Max();
        var monthlyValues = new List<MonthlyRevenue>();
        for (var month = firstMonth; month <= lastMonth; month = month.AddMonths(1))
        {
            monthlyValues.Add(new MonthlyRevenue(
                month,
                revenueByMonth.TryGetValue(month, out var revenue) ? revenue : 0m));
        }

        var points = monthlyValues
            .Select((item, index) => new MonthlySmaPoint(
                item.Month,
                item.Revenue,
                index + 1 < SmaWindow
                    ? null
                    : monthlyValues.Skip(index - SmaWindow + 1).Take(SmaWindow).Average(value => value.Revenue)))
            .ToList();

        decimal? forecast = monthlyValues.Count < SmaWindow
            ? null
            : monthlyValues.Skip(monthlyValues.Count - SmaWindow).Average(value => value.Revenue);

        return new SmaAnalysis(points, forecast);
    }
}

public sealed record ProductRevenueInput(int ShoeId, string ProductName, int Quantity, decimal UnitPrice);

public sealed record AbcProductResult(
    int ShoeId,
    string ProductName,
    decimal Revenue,
    decimal Share,
    decimal CumulativeShare,
    string Classification);

public sealed record AbcClassSummary(string ClassName, int ProductCount, decimal RevenueShare);

public sealed record AbcAnalysis(
    IReadOnlyList<AbcProductResult> Products,
    IReadOnlyList<AbcClassSummary> ClassSummaries);

public sealed record MonthlyRevenue(DateTime Month, decimal Revenue);

public sealed record MonthlySmaPoint(DateTime Month, decimal Revenue, decimal? MovingAverage);

public sealed record SmaAnalysis(
    IReadOnlyList<MonthlySmaPoint> Points,
    decimal? NextMonthForecast);
