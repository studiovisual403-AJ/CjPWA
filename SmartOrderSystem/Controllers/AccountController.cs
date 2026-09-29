using Microsoft.AspNetCore.Mvc;
using SmartOrderSystem.Data;
using SmartOrderSystem.Models;
using SmartOrderSystem.ViewModels;
using System.Linq;
using System.Collections.Generic;
using BCrypt.Net;

namespace SmartOrderSystem.Controllers
{
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true, Duration = 0)]
    public class AccountController : Controller
    {
        private const string ResetEmailKey = "ResetEmail";
        private const string ResetVerifiedAtKey = "ResetVerifiedAt";
        private static readonly TimeSpan ResetAuthorizationLifetime = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan ResetAnswerLockoutDuration = TimeSpan.FromMinutes(15);
        private const int MaxResetAnswerAttempts = 5;
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context) { _context = context; }

        [HttpGet]
        public IActionResult Login()
        {
            ViewBag.Title = "Customer Login";
            ViewBag.ShowRegister = true;
            ViewBag.ControllerName = "Account";
            return View("~/Views/Shared/_Login.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(LoginView model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Title = "Customer Login";
                ViewBag.ShowRegister = true;
                ViewBag.ControllerName = "Account";
                return View("~/Views/Shared/_Login.cshtml", model);
            }

            var customer = _context.Customers.FirstOrDefault(c => c.email == model.Email);
            var passwordValid = false;

            if (customer != null)
            {
                try { passwordValid = BCrypt.Net.BCrypt.Verify(model.Password, customer.password_hash); }
                catch (ArgumentException) { passwordValid = false; }
            }

            if (customer != null && passwordValid)
            {
                HttpContext.Session.SetString("CustomerUser", customer.email);
                HttpContext.Session.SetString("UserType", "Customer");
                return RedirectToAction("Index", "Shop");
            }

            ViewBag.Error = "The email or password is invalid.";
            ViewBag.Title = "Customer Login";
            ViewBag.ShowRegister = true;
            ViewBag.ControllerName = "Account";
            return View("~/Views/Shared/_Login.cshtml", model);
        }

        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            ViewBag.ControllerName = "Account";
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid) { ViewBag.ControllerName = "Account"; return View(model); }

            var email = model.Email.Trim();
            var customer = _context.Customers.FirstOrDefault(c => c.email == email);
            if (customer == null)
            {
                ModelState.AddModelError(string.Empty, "We could not verify that account. Check your details and try again.");
                ViewBag.ControllerName = "Account";
                return View(model);
            }
            HttpContext.Session.SetString(ResetEmailKey, customer.email);
            HttpContext.Session.Remove(ResetVerifiedAtKey);
            return RedirectToAction("SecurityQuestions");
        }

        [HttpGet]
        public IActionResult SecurityQuestions()
        {
            var email = HttpContext.Session.GetString(ResetEmailKey);
            if (string.IsNullOrEmpty(email)) { return RedirectToAction("ForgotPassword"); }
            var customer = _context.Customers.FirstOrDefault(c => c.email == email);
            if (customer == null) { return RedirectToAction("ForgotPassword"); }
            var userSecurityAnswer = _context.UserSecurityAnswers.FirstOrDefault(usa => usa.customer_id == customer.customer_id);
            if (userSecurityAnswer == null) { return RedirectToAction("ForgotPassword"); }
            var model = new SecurityQuestionViewModel { Email = customer.email, Question1Text = userSecurityAnswer.question_1, Question2Text = userSecurityAnswer.question_2 };
            return View("VerifySecurityQuestion", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult VerifyAnswers(SecurityQuestionViewModel model)
        {
            var email = HttpContext.Session.GetString(ResetEmailKey);
            if (string.IsNullOrEmpty(email)) { return RedirectToAction("ForgotPassword"); }
            var customer = _context.Customers.FirstOrDefault(c => c.email == email);
            if (customer == null) { return RedirectToAction("ForgotPassword"); }
            var userSecurityAnswer = _context.UserSecurityAnswers.FirstOrDefault(usa => usa.customer_id == customer.customer_id);
            if (userSecurityAnswer == null) { return RedirectToAction("ForgotPassword"); }
            model.Email = customer.email;
            model.Question1Text = userSecurityAnswer.question_1;
            model.Question2Text = userSecurityAnswer.question_2;
            if (!ModelState.IsValid) { return View("VerifySecurityQuestion", model); }
            if (customer.reset_answer_locked_until.HasValue && customer.reset_answer_locked_until > DateTime.UtcNow)
            {
                ModelState.AddModelError(string.Empty, "Too many incorrect attempts. Please try again later.");
                return View("VerifySecurityQuestion", model);
            }

            bool isAnswer1Correct;
            bool isAnswer2Correct;
            try
            {
                isAnswer1Correct = BCrypt.Net.BCrypt.Verify(NormalizeSecurityAnswer(model.Answer1), userSecurityAnswer.answer_1_hash);
                isAnswer2Correct = BCrypt.Net.BCrypt.Verify(NormalizeSecurityAnswer(model.Answer2), userSecurityAnswer.answer_2_hash);
            }
            catch (ArgumentException)
            {
                isAnswer1Correct = false;
                isAnswer2Correct = false;
            }

            if (isAnswer1Correct && isAnswer2Correct)
            {
                customer.reset_answer_failed_attempts = 0;
                customer.reset_answer_locked_until = null;
                _context.SaveChanges();
                HttpContext.Session.SetString(ResetVerifiedAtKey, DateTimeOffset.UtcNow.ToString("O"));
                return RedirectToAction("ResetPasswordForm");
            }

            customer.reset_answer_failed_attempts++;
            if (customer.reset_answer_failed_attempts >= MaxResetAnswerAttempts)
            {
                customer.reset_answer_failed_attempts = 0;
                customer.reset_answer_locked_until = DateTime.UtcNow.Add(ResetAnswerLockoutDuration);
                ModelState.AddModelError(string.Empty, "Too many incorrect attempts. Please try again later.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "One or both security answers are incorrect.");
            }
            _context.SaveChanges();
            return View("VerifySecurityQuestion", model);
        }

        [HttpGet]
        public IActionResult ResetPasswordForm()
        {
            var email = HttpContext.Session.GetString(ResetEmailKey);
            if (string.IsNullOrEmpty(email) || !IsResetAuthorizationValid()) { ClearResetState(); return RedirectToAction("ForgotPassword"); }
            var model = new ResetPasswordViewModel { Email = email };
            return View("ResetPassword", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdatePassword(ResetPasswordViewModel model)
        {
            var email = HttpContext.Session.GetString(ResetEmailKey);
            if (string.IsNullOrEmpty(email) || !IsResetAuthorizationValid()) { ClearResetState(); return RedirectToAction("ForgotPassword"); }
            model.Email = email;
            if (!ModelState.IsValid) { return View("ResetPassword", model); }
            var customer = _context.Customers.FirstOrDefault(c => c.email == email);
            if (customer != null)
            {
                customer.password_hash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
                _context.SaveChanges();
                ClearResetState();
                TempData["Success"] = "Password successfully reset! You can now log in.";
                return RedirectToAction("Login");
            }
            return RedirectToAction("ForgotPassword");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(string FullName, string Email, string ContactNumber, string Password)
        {
            FullName = FullName?.Trim() ?? string.Empty;
            Email = Email?.Trim() ?? string.Empty;
            ContactNumber = ContactNumber?.Trim() ?? string.Empty;
            Password ??= string.Empty;

            if (FullName.Length < 2 || FullName.Length > 100)
                ModelState.AddModelError(string.Empty, "Full name must be between 2 and 100 characters.");
            if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(Email))
                ModelState.AddModelError(string.Empty, "Enter a valid email address.");
            if (!System.Text.RegularExpressions.Regex.IsMatch(ContactNumber, @"^[0-9+()\-\s]{7,20}$"))
                ModelState.AddModelError(string.Empty, "Enter a valid contact number.");
            if (Password.Length < 8 || !Password.Any(char.IsUpper) || !Password.Any(char.IsLower) || !Password.Any(char.IsDigit))
                ModelState.AddModelError(string.Empty, "Password must be at least 8 characters and include uppercase, lowercase, and a number.");

            if (!ModelState.IsValid)
            {
                ViewBag.Title = "Customer Login";
                ViewBag.ShowRegister = true;
                ViewBag.ControllerName = "Account";
                return View("~/Views/Shared/_Login.cshtml");
            }

            if (_context.Customers.Any(c => c.email.Trim().ToLower() == Email.ToLower()))
            {
                ModelState.AddModelError(string.Empty, "Email is already registered.");
                ViewBag.Title = "Customer Login";
                ViewBag.ShowRegister = true;
                ViewBag.ControllerName = "Account";
                return View("~/Views/Shared/_Login.cshtml");
            }

            var newCustomer = new Customer
            {
                full_name = FullName,
                email = Email,
                contact_number = ContactNumber,
                password_hash = BCrypt.Net.BCrypt.HashPassword(Password)
            };

            _context.Customers.Add(newCustomer);
            _context.SaveChanges();

            TempData["Success"] = "Account successfully created! You can now log in.";
            return RedirectToAction("Login");
        }

        // ============================
        // MY ACCOUNT
        // ============================
        [HttpGet]
        public IActionResult MyAccount()
        {
            var email = HttpContext.Session.GetString("CustomerUser");
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("Login", "Account");
            }

            var customer = _context.Customers.FirstOrDefault(c => c.email == email);
            if (customer == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = new MyAccountViewModel
            {
                CustomerId = customer.customer_id,
                FullName = customer.full_name,
                Email = customer.email,
                ContactNumber = customer.contact_number,

                TotalOrders = _context.Orders.Count(o => o.customer_id == customer.customer_id),
                TotalReservations = _context.Reservations.Count(r => r.customer_id == customer.customer_id),
                SavedAddressesCount = _context.CustomerAddresses.Count(a => a.customer_id == customer.customer_id),

                RecentOrders = _context.Orders
                    .Where(o => o.customer_id == customer.customer_id)
                    .OrderByDescending(o => o.order_date)
                    .Take(5)
                    .Select(o => new RecentOrderItem
                    {
                        OrderNumber = "#CJ" + o.order_id.ToString("D6"),
                        OrderDate = o.order_date,
                        ItemsCount = o.OrderItems.Count(),
                        Total = o.total_amount,
                        Status = o.status
                    })
                    .ToList()
            };

            return View(model);
        }

        [HttpGet]
        public IActionResult MyAddresses()
        {
            var email = HttpContext.Session.GetString("CustomerUser");
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("Login", "Account");
            }

            var customer = _context.Customers.FirstOrDefault(c => c.email == email);
            if (customer == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var addresses = _context.CustomerAddresses
                .Where(a => a.customer_id == customer.customer_id)
                .OrderByDescending(a => a.is_default)
                .ThenByDescending(a => a.updated_at)
                .ToList();

            return View(addresses);
        }

        [HttpGet]
        public IActionResult AddAddress()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("CustomerUser")))
                return RedirectToAction("Login", "Account");
            return RedirectToAction(nameof(MyAddresses));
        }

        // ============================
        // ADDRESS: GET single (for Edit modal, AJAX)
        // ============================
        [HttpGet]
        public IActionResult GetAddress(int id)
        {
            var email = HttpContext.Session.GetString("CustomerUser");
            if (string.IsNullOrEmpty(email)) return Unauthorized();

            var customer = _context.Customers.FirstOrDefault(c => c.email == email);
            if (customer == null) return Unauthorized();

            var address = _context.CustomerAddresses
                .FirstOrDefault(a => a.address_id == id && a.customer_id == customer.customer_id);

            if (address == null) return NotFound();

            return Json(new
            {
                addressId = address.address_id,
                addressType = address.address_type,
                recipientName = address.recipient_name,
                contactNumber = address.contact_number,
                province = address.province,
                city = address.city,
                barangay = address.barangay,
                streetAddress = address.street_address,
                postalCode = address.postal_code,
                landmark = address.landmark
            });
        }

        // ============================
        // ADDRESS: Add / Edit (same form, iisang modal)
        // ============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveAddress(AddressFormViewModel form)
        {
            var email = HttpContext.Session.GetString("CustomerUser");
            if (string.IsNullOrEmpty(email)) return RedirectToAction("Login", "Account");

            var customer = _context.Customers.FirstOrDefault(c => c.email == email);
            if (customer == null) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please fill in all required address fields.";
                return RedirectToAction("MyAddresses");
            }

            if (form.AddressId > 0)
            {
                var existing = _context.CustomerAddresses
                    .FirstOrDefault(a => a.address_id == form.AddressId && a.customer_id == customer.customer_id);

                if (existing == null) return NotFound();

                existing.address_type = form.AddressType;
                existing.recipient_name = form.RecipientName;
                existing.contact_number = form.ContactNumber;
                existing.province = form.Province;
                existing.city = form.City;
                existing.barangay = form.Barangay;
                existing.street_address = form.StreetAddress;
                existing.postal_code = form.PostalCode;
                existing.landmark = form.Landmark;
                existing.updated_at = DateTime.Now;

                _context.SaveChanges();
                TempData["Success"] = "Address updated successfully!";
            }
            else
            {
                var hasAnyAddress = _context.CustomerAddresses.Any(a => a.customer_id == customer.customer_id);

                var address = new CustomerAddress
                {
                    customer_id = customer.customer_id,
                    address_type = form.AddressType,
                    recipient_name = form.RecipientName,
                    contact_number = form.ContactNumber,
                    province = form.Province,
                    city = form.City,
                    barangay = form.Barangay,
                    street_address = form.StreetAddress,
                    postal_code = form.PostalCode,
                    landmark = form.Landmark,
                    is_default = !hasAnyAddress, // unang address ng customer, gawing default
                    created_at = DateTime.Now,
                    updated_at = DateTime.Now
                };

                _context.CustomerAddresses.Add(address);
                _context.SaveChanges();
                TempData["Success"] = "Address added successfully!";
            }

            return RedirectToAction("MyAddresses");
        }

        // ============================
        // ADDRESS: Delete
        // ============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteAddress(int id)
        {
            var email = HttpContext.Session.GetString("CustomerUser");
            if (string.IsNullOrEmpty(email)) return RedirectToAction("Login", "Account");

            var customer = _context.Customers.FirstOrDefault(c => c.email == email);
            if (customer == null) return RedirectToAction("Login", "Account");

            var address = _context.CustomerAddresses
                .FirstOrDefault(a => a.address_id == id && a.customer_id == customer.customer_id);

            if (address == null) return NotFound();

            var wasDefault = address.is_default;

            try
            {
                _context.CustomerAddresses.Remove(address);
                _context.SaveChanges();
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException)
            {
                // Malamang may existing order na naka-link sa address_id na ito (foreign key constraint)
                TempData["Error"] = "This address can't be deleted because it's linked to an existing order.";
                return RedirectToAction("MyAddresses");
            }

            // Kung na-delete yung default, gawing default na lang ang pinakabagong natira
            if (wasDefault)
            {
                var next = _context.CustomerAddresses
                    .Where(a => a.customer_id == customer.customer_id)
                    .OrderByDescending(a => a.updated_at)
                    .FirstOrDefault();

                if (next != null)
                {
                    next.is_default = true;
                    _context.SaveChanges();
                }
            }

            TempData["Success"] = "Address deleted successfully!";
            return RedirectToAction("MyAddresses");
        }

        // ============================
        // ADDRESS: Set as Default
        // ============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetDefaultAddress(int id)
        {
            var email = HttpContext.Session.GetString("CustomerUser");
            if (string.IsNullOrEmpty(email)) return RedirectToAction("Login", "Account");

            var customer = _context.Customers.FirstOrDefault(c => c.email == email);
            if (customer == null) return RedirectToAction("Login", "Account");

            var addresses = _context.CustomerAddresses
                .Where(a => a.customer_id == customer.customer_id)
                .ToList();

            var target = addresses.FirstOrDefault(a => a.address_id == id);
            if (target == null) return NotFound();

            foreach (var a in addresses)
            {
                a.is_default = (a.address_id == id);
            }

            _context.SaveChanges();
            TempData["Success"] = "Default address updated!";
            return RedirectToAction("MyAddresses");
        }

        // ============================
        // PRIVATE HELPERS
        // ============================
        private bool IsResetAuthorizationValid()
        {
            var verifiedAt = HttpContext.Session.GetString(ResetVerifiedAtKey);
            return DateTimeOffset.TryParse(verifiedAt, out var timestamp) && DateTimeOffset.UtcNow - timestamp <= ResetAuthorizationLifetime;
        }

        private void ClearResetState()
        {
            HttpContext.Session.Remove(ResetEmailKey);
            HttpContext.Session.Remove(ResetVerifiedAtKey);
        }

        private static string NormalizeSecurityAnswer(string answer) => answer.Trim();

        [HttpGet]
        public IActionResult EditProfile()
        {
            var email = HttpContext.Session.GetString("CustomerUser");
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("Login", "Account");
            }

            var customer = _context.Customers.FirstOrDefault(c => c.email == email);
            if (customer == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = new EditProfileViewModel
            {
                CustomerId = customer.customer_id,
                FullName = customer.full_name,
                Email = customer.email,
                ContactNumber = customer.contact_number
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditProfile(EditProfileViewModel model)
        {
            var email = HttpContext.Session.GetString("CustomerUser");
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("Login", "Account");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var customer = _context.Customers.FirstOrDefault(c => c.email == email);
            if (customer == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (!string.Equals(customer.email, model.Email, StringComparison.OrdinalIgnoreCase)
                && _context.Customers.Any(c => c.email == model.Email))
            {
                ModelState.AddModelError(nameof(model.Email), "Email is already in use by another account.");
                return View(model);
            }

            customer.full_name = model.FullName;
            customer.email = model.Email;
            customer.contact_number = model.ContactNumber;
            _context.SaveChanges();

            HttpContext.Session.SetString("CustomerUser", customer.email);

            TempData["Success"] = "Profile updated successfully!";
            return RedirectToAction("MyAccount");
        }

        // ============================
        // CHANGE PASSWORD
        // ============================
        [HttpGet]
        public IActionResult ChangePassword()
        {
            var email = HttpContext.Session.GetString("CustomerUser");
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("Login", "Account");
            }

            return View(new ChangePasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ChangePassword(ChangePasswordViewModel model)
        {
            var email = HttpContext.Session.GetString("CustomerUser");
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("Login", "Account");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var customer = _context.Customers.FirstOrDefault(c => c.email == email);
            if (customer == null)
            {
                return RedirectToAction("Login", "Account");
            }

            bool currentPasswordValid;
            try { currentPasswordValid = BCrypt.Net.BCrypt.Verify(model.CurrentPassword, customer.password_hash); }
            catch (ArgumentException) { currentPasswordValid = false; }

            if (!currentPasswordValid)
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "Current password is incorrect.");
                return View(model);
            }

            customer.password_hash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            _context.SaveChanges();

            HttpContext.Session.Clear();
            TempData["Success"] = "Password changed successfully! Please log in again.";
            return RedirectToAction("Login");
        }
    }
}