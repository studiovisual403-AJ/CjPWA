using System;
using System.Collections.Generic;

namespace SmartOrderSystem.ViewModels
{
    public class SalesDetailsViewModel
    {
        public DateTime DateStart { get; set; }
        public DateTime DateEnd { get; set; }
        public string? SelectedBrand { get; set; }
        public string? SelectedCategory { get; set; }
        public string? SelectedStatus { get; set; }

        public List<string> BrandOptions { get; set; } = new();
        public List<string> CategoryOptions { get; set; } = new();
        public List<string> StatusOptions { get; set; } = new()
        {
            "Pending", "Confirmed", "Preparing", "In Transit", "Delivered", "Completed", "Cancelled"
        };

        public decimal TotalSales { get; set; }
        public double TotalSalesGrowth { get; set; }

        public int CompletedOrders { get; set; }
        public double CompletedOrdersGrowth { get; set; }

        public int TotalProductsSold { get; set; }
        public double TotalProductsSoldGrowth { get; set; }

        public decimal AverageOrderValue { get; set; }
        public double AverageOrderValueGrowth { get; set; }

        public string BestSellingProductName { get; set; } = "N/A";
        public int BestSellingProductUnits { get; set; }

        public List<MonthlyTrendItem> MonthlySalesTrend { get; set; } = new();
        public List<BrandSalesItem> BrandSales { get; set; } = new();
        public List<CategorySalesItem> CategorySales { get; set; } = new();
        public List<OrderStatusItem> OrderStatusDistribution { get; set; } = new();

        public List<BestSellingProductItem> BestSellingProducts { get; set; } = new();
        public List<SalesTransactionItem> Transactions { get; set; } = new();

        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 5;
        public int TotalTransactions { get; set; }
        public int TotalPages => TotalTransactions == 0 ? 1 : (int)Math.Ceiling(TotalTransactions / (double)PageSize);
    }

    public class MonthlyTrendItem
    {
        public string Month { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class BrandSalesItem
    {
        public string Brand { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public double Percentage { get; set; }
        public string Color { get; set; } = "#3b82f6";
    }

    public class CategorySalesItem
    {
        public string Category { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class OrderStatusItem
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Percentage { get; set; }
        public string Color { get; set; } = "#22c55e";
    }

    public class BestSellingProductItem
    {
        public int Rank { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public int UnitsSold { get; set; }
        public decimal Revenue { get; set; }
    }

    public class SalesTransactionItem
    {
        public string OrderCode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int Qty { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? PaymentMethod { get; set; }
    }
}