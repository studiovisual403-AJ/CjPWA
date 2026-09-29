using System;
using System.Collections.Generic;

namespace SmartOrderSystem.ViewModels
{
    public class MyAccountViewModel
    {
        // Profile (galing sa Customer table)
        public int CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;

        // Stats — TODO: palitan ng totoong query pag meron nang Orders/Reservations/Addresses tables
        public int TotalOrders { get; set; }
        public int TotalReservations { get; set; }
        public int SavedAddressesCount { get; set; }

        public List<RecentOrderItem> RecentOrders { get; set; } = new();
    }

    public class RecentOrderItem
    {
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public int ItemsCount { get; set; }
        public decimal Total { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}