using Microsoft.AspNetCore.Mvc;
using SmartOrderSystem.Data;
using SmartOrderSystem.Services;

namespace SmartOrderSystem.Controllers
{
    public class AdminAlertsController : AdminBaseController
    {
        private readonly IAdminAlertService _adminAlertService;
        private readonly ILogger<AdminAlertsController> _logger;

        public AdminAlertsController(
            ApplicationDbContext context,
            IAdminAlertService adminAlertService,
            ILogger<AdminAlertsController> logger) : base(context)
        {
            _adminAlertService = adminAlertService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Summary()
        {
            try
            {
                return Json(await _adminAlertService.GetSummaryAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load admin alert summary.");
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }
    }
}
