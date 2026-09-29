using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SmartOrderSystem.Data;

namespace SmartOrderSystem.Controllers
{
    public class AdminBaseController : Controller
    {
        private readonly ApplicationDbContext _context;

        protected AdminBaseController(ApplicationDbContext context)
        {
            _context = context;
        }

        public override async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            var email = HttpContext.Session.GetString("AdminUser");
            var userType = HttpContext.Session.GetString("UserType");
            var isValidAdmin = userType == "Admin"
                && !string.IsNullOrWhiteSpace(email)
                && await _context.Admins.AnyAsync(a => a.email == email);

            if (!isValidAdmin)
            {
                HttpContext.Session.Clear();
                context.Result = RedirectToAction("Login", "AdminAccount");
                return;
            }

            await next();
        }
    }
}
