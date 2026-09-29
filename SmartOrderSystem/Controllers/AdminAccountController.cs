using Microsoft.AspNetCore.Mvc;
using SmartOrderSystem.Data;
using SmartOrderSystem.Models;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;

namespace SmartOrderSystem.Controllers
{
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true, Duration = 0)]
    public class AdminAccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        public AdminAccountController(ApplicationDbContext context) { _context = context; }

        [HttpGet]
        public IActionResult Login()
        {
            ViewBag.Title = "Admin Login";
            ViewBag.ShowRegister = false; // 1. ITATAGO SI REGISTER
            ViewBag.ControllerName = "AdminAccount";
            return View("~/Views/Shared/_Login.cshtml"); // 2. PAREHONG VIEW GAMIT
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(LoginView model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Title = "Admin Login";
                ViewBag.ShowRegister = false;
                ViewBag.ControllerName = "AdminAccount";
                return View("~/Views/Shared/_Login.cshtml", model);
            }

            // 3. CHECK SA ADMINS TABLE LANG TO
            var admin = _context.Admins.FirstOrDefault(a => a.email == model.Email);
            var passwordValid = false;

            if (admin!= null)
            {
                try { passwordValid = BCrypt.Net.BCrypt.Verify(model.Password, admin.password_hash); }
                catch (ArgumentException) { passwordValid = false; }

            }

            if (admin!= null && passwordValid)
            {
                HttpContext.Session.SetString("AdminUser", admin.email);
                HttpContext.Session.SetString("UserType", "Admin");
                return RedirectToAction("Index", "Dashboard");
            }

            ViewBag.Error = "Invalid email or password.";
            ViewBag.Title = "Admin Login";
            ViewBag.ShowRegister = false;
            ViewBag.ControllerName = "AdminAccount";
            return View("~/Views/Shared/_Login.cshtml", model);
        }

        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return NotFound();
        }

        [HttpGet]
        public async Task<IActionResult> Notifications()
        {
            if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString("AdminUser")))
                return RedirectToAction(nameof(Login));

            var notifications = await _context.Notifications
                .Where(n => n.customer_id == null)
                .OrderByDescending(n => n.created_at)
                .Take(50)
                .ToListAsync();
            return View(notifications);
        }

        [HttpGet]
        public IActionResult Settings()
        {
            if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString("AdminUser")))
                return RedirectToAction(nameof(Login));
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var email = HttpContext.Session.GetString("AdminUser");
            if (string.IsNullOrWhiteSpace(email))
                return RedirectToAction(nameof(Login));
            var admin = await _context.Admins.FirstOrDefaultAsync(a => a.email == email);
            return admin == null ? RedirectToAction(nameof(Login)) : View(admin);
        }
    }
}