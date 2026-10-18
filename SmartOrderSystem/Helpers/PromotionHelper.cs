using SmartOrderSystem.Models;

namespace SmartOrderSystem.Helpers
{
    public static class PromotionHelper
    {
        public static decimal GetEffectivePrice(ShoeCatalog shoe)
        {
            return GetEffectivePrice(shoe.default_price, GetCurrentPromotion(shoe));
        }

        public static (decimal UnitPrice, decimal Subtotal) CalculatePrice(ShoeCatalog shoe, int quantity)
        {
            var unitPrice = GetEffectivePrice(shoe);
            return (unitPrice, unitPrice * quantity);
        }

        public static ProductPromotion? GetCurrentPromotion(ShoeCatalog shoe)
        {
            var today = DateTime.Today;
            return shoe.Promotions?
                .Where(p => p.status == "Active" && today >= p.start_date.Date && today <= p.end_date.Date)
                .OrderByDescending(p => p.promotion_id)
                .FirstOrDefault();
        }

        public static decimal GetEffectivePrice(decimal originalPrice, ProductPromotion? promo)
        {
            if (promo == null) return originalPrice;
            return originalPrice - (originalPrice * promo.discount_percentage / 100m);
        }
    }
}