using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOrderSystem.Data;

namespace SmartOrderSystem.Controllers
{
    public class NotificationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NotificationController(ApplicationDbContext context)
        {
            _context = context;
        }

        private async Task<int?> GetCurrentCustomerIdAsync()
        {
            var email = HttpContext.Session.GetString("CustomerUser");
            if (string.IsNullOrWhiteSpace(email))
                return null;

            return await _context.Customers
                .Where(c => c.email == email)
                .Select(c => (int?)c.customer_id)
                .FirstOrDefaultAsync();
        }

        [HttpGet]
        public async Task<IActionResult> GetRecent()
        {
            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId == null)
                return Json(new { success = false, message = "Not logged in.", notifications = Array.Empty<object>(), unreadCount = 0 });

            var notifications = await _context.Notifications
                .Where(n => n.customer_id == customerId)
                .OrderByDescending(n => n.created_at)
                .Take(15)
                .Select(n => new
                {
                    id = n.notification_id,
                    n.title,
                    n.message,
                    n.type,
                    n.is_read,
                    n.created_at
                })
                .ToListAsync();

            var unreadCount = await _context.Notifications
                .CountAsync(n => n.customer_id == customerId && !n.is_read);

            return Json(new { success = true, notifications, unreadCount });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId == null)
                return Json(new { success = false, message = "Not logged in." });

            var unread = await _context.Notifications
                .Where(n => n.customer_id == customerId && !n.is_read)
                .ToListAsync();

            foreach (var n in unread)
                n.is_read = true;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}