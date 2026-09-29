using Microsoft.EntityFrameworkCore;
using SmartOrderSystem.Data;
using SmartOrderSystem.Models;

namespace SmartOrderSystem.Services
{
    public interface INotificationService
    {
        Task NotifyCustomerAsync(int? customerId, string type, string title, string message,
            int? orderId = null, int? promotionId = null, int? shoeId = null);

        Task NotifyAllCustomersAsync(string type, string title, string message,
            int? promotionId = null, int? shoeId = null);
    }

    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(ApplicationDbContext context, ILogger<NotificationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task NotifyCustomerAsync(int? customerId, string type, string title, string message,
            int? orderId = null, int? promotionId = null, int? shoeId = null)
        {
            if (customerId == null) return;

            try
            {
                _context.Notifications.Add(new Notification
                {
                    customer_id = customerId,
                    order_id = orderId,
                    promotion_id = promotionId,
                    shoe_id = shoeId,
                    type = type,
                    title = title,
                    message = message,
                    created_at = DateTime.Now,
                    is_read = false
                });
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Best-effort lang ang notification - huwag ipa-fail ang core operation
                _logger.LogError(ex, "Failed to notify customer {CustomerId}", customerId);
            }
        }

        public async Task NotifyAllCustomersAsync(string type, string title, string message,
            int? promotionId = null, int? shoeId = null)
        {
            try
            {
                var customerIds = await _context.Customers.Select(c => c.customer_id).ToListAsync();
                var now = DateTime.Now;

                foreach (var id in customerIds)
                {
                    _context.Notifications.Add(new Notification
                    {
                        customer_id = id,
                        promotion_id = promotionId,
                        shoe_id = shoeId,
                        type = type,
                        title = title,
                        message = message,
                        created_at = now,
                        is_read = false
                    });
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast notification (type={Type})", type);
            }
        }
    }
}