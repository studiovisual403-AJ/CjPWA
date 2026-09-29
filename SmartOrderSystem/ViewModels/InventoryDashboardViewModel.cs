using System.Collections.Generic;

namespace SmartOrderSystem.ViewModels // Ginawa nating .ViewModels para saktong tumugma sa folder mo
{
    public class InventoryDashboardViewModel
    {
        public int TotalItems { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }
        public int TotalProducts { get; set; }

        public List<decimal> SizeColumns { get; set; } = new List<decimal>
        {
            36, 37, 38, 39, 40, 41, 42, 43, 44, 45
        };

        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
        public List<InventoryMatrixRow> MatrixRows { get; set; } = new List<InventoryMatrixRow>();
    }

    public class InventoryMatrixRow
    {
        public int ShoeId { get; set; }
        public string Brand { get; set; }
        public string ModelName { get; set; }
        public string Sku { get; set; }

        // Key = size, Value = quantity in stock (null if no record exists for that size)
        public Dictionary<decimal, int?> StockBySize { get; set; } = new Dictionary<decimal, int?>();
    }
}