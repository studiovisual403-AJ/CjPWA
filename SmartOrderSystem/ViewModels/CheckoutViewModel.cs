using System;

namespace SmartOrderSystem.ViewModels
{
    // Session-persisted state habang tumatawid sa 3 steps
    public class CheckoutSessionState
    {
        // Reservation-based path (existing)
        public int? ReservationId { get; set; }

        // Direct buy-now path (bago)
        public int? InventoryId { get; set; }
        public int? Quantity { get; set; }
        public List<int> CartItemIds { get; set; } = new();

        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Province { get; set; }
        public string? City { get; set; }
        public string? Barangay { get; set; }
        public string? Street { get; set; }
        public string? Landmark { get; set; }
        public bool DeliveryCompleted { get; set; }
        public string? PaymentMethod { get; set; }
        public bool PaymentCompleted { get; set; }
        public int? CreatedOrderId { get; set; }
    }

    public class CheckoutOrderSummaryViewModel
    {
        public List<CheckoutCartItemViewModel> Items { get; set; } = new();
        public string ShoeName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
        public decimal Size { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal Total { get; set; }
    }

    public class CheckoutCartItemViewModel
    {
        public string ShoeName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
        public string Size { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal => UnitPrice * Quantity;
    }

    public class CheckoutDeliveryViewModel
    {
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Province { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Barangay { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string? Landmark { get; set; }

        public CheckoutOrderSummaryViewModel OrderSummary { get; set; } = new();

        // Para sa Change Address modal - listahan ng saved addresses ng customer
        // at kung alin dito ang kasalukuyang napili para sa order na ito
        public List<SmartOrderSystem.Models.CustomerAddress> SavedAddresses { get; set; } = new();
        public int? SelectedAddressId { get; set; }
    }

    public class CheckoutPaymentViewModel
    {
        public string PaymentMethod { get; set; } = "Cash on Delivery";
        public CheckoutOrderSummaryViewModel OrderSummary { get; set; } = new();
    }

    public class CheckoutConfirmationViewModel
    {
        public int OrderId { get; set; }
        public string DisplayOrderId => $"#ORD-{OrderId:D4}";
        public DateTime OrderDate { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }

        public string ShoeName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
        public decimal Size { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}