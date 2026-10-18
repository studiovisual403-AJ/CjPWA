using SmartOrderSystem.Helpers;

namespace SmartOrderSystem.Tests;

public class ReportAnalyticsTests
{
    [Fact]
    public void AnalyzeAbc_ClassifiesUsingCumulativeRevenueBeforeEachProduct()
    {
        var result = ReportAnalytics.AnalyzeAbc(new[]
        {
            new ProductRevenueInput(1, "Alpha", 1, 30m),
            new ProductRevenueInput(1, "Alpha", 1, 50m),
            new ProductRevenueInput(2, "Beta", 1, 15m),
            new ProductRevenueInput(3, "Gamma", 1, 5m)
        });

        Assert.Equal(new[] { "A", "B", "C" }, result.Products.Select(product => product.Classification));
        Assert.Equal(new[] { 0.80m, 0.95m, 1m }, result.Products.Select(product => product.CumulativeShare));
        Assert.Equal(new[] { 1, 1, 1 }, result.ClassSummaries.Select(summary => summary.ProductCount));
        Assert.Equal(new[] { 0.80m, 0.15m, 0.05m }, result.ClassSummaries.Select(summary => summary.RevenueShare));
    }

    [Fact]
    public void AnalyzeAbc_KeepsLargestProductInClassAAndSortsTiesByName()
    {
        var result = ReportAnalytics.AnalyzeAbc(new[]
        {
            new ProductRevenueInput(1, "Zebra", 1, 10m),
            new ProductRevenueInput(2, "Beta", 1, 45m),
            new ProductRevenueInput(3, "Alpha", 1, 45m),
            new ProductRevenueInput(4, "No Sale", 0, 100m)
        });

        Assert.Equal(new[] { "Alpha", "Beta", "Zebra" }, result.Products.Select(product => product.ProductName));
        Assert.Equal("A", result.Products[0].Classification);
    }

    [Fact]
    public void AnalyzeAbc_ReturnsEmptyProductsWhenTotalRevenueIsZero()
    {
        var result = ReportAnalytics.AnalyzeAbc(new[]
        {
            new ProductRevenueInput(1, "Alpha", 0, 100m)
        });

        Assert.Empty(result.Products);
        Assert.Equal(new[] { "A", "B", "C" }, result.ClassSummaries.Select(summary => summary.ClassName));
        Assert.All(result.ClassSummaries, summary => Assert.Equal(0m, summary.RevenueShare));
    }

    [Fact]
    public void CalculateSma_UsesFullThreeMonthWindowsAndZeroSalesMonths()
    {
        var result = ReportAnalytics.CalculateSma(new[]
        {
            new MonthlyRevenue(new DateTime(2026, 1, 1), 300m),
            new MonthlyRevenue(new DateTime(2026, 2, 1), 0m),
            new MonthlyRevenue(new DateTime(2026, 3, 1), 600m),
            new MonthlyRevenue(new DateTime(2026, 4, 1), 900m)
        });

        Assert.Null(result.Points[0].MovingAverage);
        Assert.Null(result.Points[1].MovingAverage);
        Assert.Equal(300m, result.Points[2].MovingAverage);
        Assert.Equal(500m, result.Points[3].MovingAverage);
        Assert.Equal(500m, result.NextMonthForecast);
    }

    [Fact]
    public void CalculateSma_ReturnsNullBeforeAFullWindowAndFillsMissingMonthsWithZero()
    {
        var result = ReportAnalytics.CalculateSma(new[]
        {
            new MonthlyRevenue(new DateTime(2026, 1, 1), 300m),
            new MonthlyRevenue(new DateTime(2026, 3, 1), 600m)
        });

        Assert.Equal(3, result.Points.Count);
        Assert.Equal(0m, result.Points[1].Revenue);
        Assert.Equal(300m, result.Points[2].MovingAverage);
        Assert.Equal(300m, result.NextMonthForecast);
    }

    [Fact]
    public void CalculateSma_ReturnsNullForecastWhenFewerThanThreeMonthsExist()
    {
        var result = ReportAnalytics.CalculateSma(new[]
        {
            new MonthlyRevenue(new DateTime(2026, 1, 1), 300m),
            new MonthlyRevenue(new DateTime(2026, 2, 1), 600m)
        });

        Assert.All(result.Points, point => Assert.Null(point.MovingAverage));
        Assert.Null(result.NextMonthForecast);
    }

    [Fact]
    public void AnalyzeAbc_WorkedExample_MatchesExpectedClasses()
    {
        var result = ReportAnalytics.AnalyzeAbc(new[]
        {
            new ProductRevenueInput(1, "P1", 1, 55m),
            new ProductRevenueInput(2, "P2", 1, 25m),
            new ProductRevenueInput(3, "P3", 1, 10m),
            new ProductRevenueInput(4, "P4", 1, 6m),
            new ProductRevenueInput(5, "P5", 1, 4m)
        });

        Assert.Equal(new[] { "A", "A", "B", "B", "C" }, result.Products.Select(product => product.Classification));
        Assert.Equal(new[] { 2, 2, 1 }, result.ClassSummaries.Select(summary => summary.ProductCount));
        Assert.Equal(new[] { 0.80m, 0.16m, 0.04m }, result.ClassSummaries.Select(summary => summary.RevenueShare));
    }

    [Fact]
    public void AnalyzeAbc_KeepsDominantProductInClassA()
    {
        var result = ReportAnalytics.AnalyzeAbc(new[]
        {
            new ProductRevenueInput(1, "Big", 1, 90m),
            new ProductRevenueInput(2, "Small", 1, 10m)
        });

        Assert.Equal(new[] { "A", "B" }, result.Products.Select(product => product.Classification));
    }
}
