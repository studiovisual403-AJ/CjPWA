using System.Collections.ObjectModel;

namespace SmartOrderSystem.Services
{
    public static class AdminAlertStatuses
    {
        public const string Pending = "Pending";
        public const string Confirmed = "Confirmed";
        public const string Cancelled = "Cancelled";
        public const string OutOfStock = "Out of Stock";
        public const string ReorderNow = "Reorder Now";
        public const string Healthy = "Healthy";
        public const string NoDemandData = "No Demand Data";

        public static ReadOnlyCollection<string> WaitingOrderStatuses { get; } =
            Array.AsReadOnly(new[] { Pending, Confirmed });
    }
}
