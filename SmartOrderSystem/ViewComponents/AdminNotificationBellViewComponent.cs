using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOrderSystem.Data;
using SmartOrderSystem.Services;

namespace SmartOrderSystem.ViewComponents
{
    public class AdminNotificationBellViewComponent : ViewComponent
    {
        private readonly IAdminAlertService _adminAlertService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminNotificationBellViewComponent> _logger;

        public AdminNotificationBellViewComponent(
            IAdminAlertService adminAlertService,
            ApplicationDbContext context,
            ILogger<AdminNotificationBellViewComponent> logger)
        {
            _adminAlertService = adminAlertService;
            _context = context;
            _logger = logger;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            AdminAlertSummaryDto summary;
            try
            {
                var email = HttpContext.Session.GetString("AdminUser");
                var userType = HttpContext.Session.GetString("UserType");
                if (userType != "Admin" ||
                    string.IsNullOrWhiteSpace(email) ||
                    !await _context.Admins.AsNoTracking().AnyAsync(admin => admin.email == email))
                {
                    return View(new AdminAlertSummaryDto());
                }

                summary = await _adminAlertService.GetSummaryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to render the admin notification bell summary.");
                summary = new AdminAlertSummaryDto();
            }

            return View(summary);
        }
    }
}
