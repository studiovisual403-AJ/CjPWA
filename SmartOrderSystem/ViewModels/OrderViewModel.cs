using System;
using System.Collections.Generic;

namespace SmartOrderSystem.ViewModels
{
    // Para sa "My Orders" list (Index) - isang card per order
    public class CustomerOrderCardViewModel
    {
        public int OrderId { get; set; }
        public string DisplayOrderId => $"#ORD-{OrderId:D4}";

        public DateTime OrderDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = "Pending";

        // Preview lang ng unang item (para sa list card, gaya ng mockup)
        public string? FirstItemName { get; set; }
        public string? FirstItemImagePath { get; set; }
        public string? FirstItemBrand { get; set; }
        public decimal? FirstItemSize { get; set; }
        public int FirstItemQuantity { get; set; }
        public int TotalDistinctItems { get; set; }
    }

    public class CustomerOrderListViewModel
    {
        public string CurrentTab { get; set; } = "All";
        public string CurrentSort { get; set; } = "recent";

        public int AllCount { get; set; }
        public int PendingCount { get; set; }
        public int ConfirmedCount { get; set; }
        public int PreparingCount { get; set; }
        public int InTransitCount { get; set; }
        public int DeliveredCount { get; set; }
        public int CompletedCount { get; set; }
        public int CancelledCount { get; set; }

        public List<CustomerOrderCardViewModel> Orders { get; set; } = new();
    }

    // Para sa Order Details (customer side) - may timeline
    public class CustomerOrderItemViewModel
    {
        public int ShoeId { get; set; }
        public string ShoeName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
        public decimal Size { get; set; }
        public int Quantity { get; set; }
        public decimal ItemPrice { get; set; }
        public int? ExistingRating { get; set; }
        public decimal LineTotal => ItemPrice * Quantity;
    }

    public class CustomerOrderDetailsViewModel
    {
        public int OrderId { get; set; }
        public string DisplayOrderId => $"#ORD-{OrderId:D4}";

        public DateTime OrderDate { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public string Status { get; set; } = "Pending";
        public string? PaymentMethod { get; set; }
        public string PaymentStatus { get; set; } = "Unpaid";
        public string? Courier { get; set; }
        public string? TrackingNumber { get; set; }
        public string? CancellationReason { get; set; }

        public decimal TotalAmount { get; set; }

        public List<CustomerOrderItemViewModel> Items { get; set; } = new();

        public string ReturnTab { get; set; } = "All";
        public string ReturnSort { get; set; } = "recent";
    }
}