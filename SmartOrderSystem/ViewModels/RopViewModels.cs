namespace SmartOrderSystem.ViewModels
{
    public class RopRowViewModel
    {
        public int InventoryId { get; set; }
        public string? ImageUrl { get; set; }
        public string Brand { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public decimal Size { get; set; }
        public int Stock { get; set; }
        public double AvgDailyDemand { get; set; }
        public int SafetyStock { get; set; }
        public int ReorderPoint { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class RopDashboardViewModel
    {
        public List<RopRowViewModel> Rows { get; set; } = new();
        public string CurrentStatus { get; set; } = "All";
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int OutOfStockCount { get; set; }
        public int ReorderNowCount { get; set; }
        public int HealthyCount { get; set; }
        public int NoDataCount { get; set; }
        public int LookbackDays { get; set; }
        public int LeadTimeDays { get; set; }
        public double ServiceZ { get; set; }
    }
}