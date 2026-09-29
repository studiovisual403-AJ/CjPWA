using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOrderSystem.Data;
using SmartOrderSystem.Models;
using SmartOrderSystem.Helpers;
using System.Data;

namespace SmartOrderSystem.Controllers
{
    public class ReservationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReservationController(ApplicationDbContext context)
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

        private async Task UpdateExpiredReservationsAsync()
        {
            var expiredReservations = await _context.Reservations
                .Where(r => r.status == "Pending"
                    && r.expires_at.HasValue
                    && r.expires_at <= DateTime.Now)
                .ToListAsync();

            if (!expiredReservations.Any())
                return;

            foreach (var reservation in expiredReservations)
            {
                reservation.status = "Expired";
            }

            await _context.SaveChangesAsync();
        }

        private async Task<int> GetAvailableQuantityAsync(int inventoryId)
        {
            var inventoryQuantity = await _context.ShoeInventories
                .Where(i => i.inventory_id == inventoryId)
                .Select(i => (int?)i.quantity_in_stock)
                .FirstOrDefaultAsync() ?? 0;
            var heldQuantity = await _context.Reservations
                .Where(r => r.inventory_id == inventoryId && r.status == "Pending" &&
                            r.expires_at.HasValue && r.expires_at > DateTime.Now)
                .SumAsync(r => (int?)r.quantity) ?? 0;
            return Math.Max(0, inventoryQuantity - heldQuantity);
        }

        // GET: /Reservation - "My Reservations" page
        public async Task<IActionResult> Index(string? tab, string? sort)
        {
            await UpdateExpiredReservationsAsync();

            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId == null)
                return RedirectToAction("Login", "Account");

            var normalizedTab = string.IsNullOrWhiteSpace(tab) ? "All" : tab;
            var normalizedSort = string.IsNullOrWhiteSpace(sort) ? "recent" : sort;

            var allReservations = await _context.Reservations
                .Include(r => r.ShoeInventory)
                    .ThenInclude(si => si!.ShoeCatalog)
                .Where(r => r.customer_id == customerId)
                .Select(r => new CustomerReservationViewModel
                {
                    ReservationId = r.reservation_id,
                    ShoeName = r.ShoeInventory!.ShoeCatalog!.model_name,
                    Brand = r.ShoeInventory.ShoeCatalog.brand,
                    Category = r.ShoeInventory.ShoeCatalog.category,
                    ImagePath = r.ShoeInventory.ShoeCatalog.image_path,
                    Size = r.ShoeInventory.size,
                    Quantity = r.quantity,
                    Price = PromotionHelper.GetEffectivePrice(
                        r.ShoeInventory.ShoeCatalog.default_price,
                        PromotionHelper.GetCurrentPromotion(r.ShoeInventory.ShoeCatalog)),
                    Status = r.status,
                    ReservationDate = r.reservation_date,
                    ExpiresAt = r.expires_at,
                    ApprovedAt = r.approved_at
                })
                .ToListAsync();

            var vm = new ReservationListViewModel
            {
                CurrentTab = normalizedTab,
                CurrentSort = normalizedSort,
                AllCount = allReservations.Count,
                ActiveCount = allReservations.Count(r => r.Status == "Pending" || r.Status == "Approved"),
                CompletedCount = allReservations.Count(r => r.Status == "Converted"),
                ExpiredCount = allReservations.Count(r => r.Status == "Expired"),
                CancelledCount = allReservations.Count(r => r.Status == "Cancelled")
            };

            var active = allReservations.Where(r => r.Status == "Pending" || r.Status == "Approved");
            var history = allReservations.Where(r => r.Status != "Pending" && r.Status != "Approved");

            if (normalizedTab == "Active")
            {
                history = Enumerable.Empty<CustomerReservationViewModel>();
            }
            else if (normalizedTab == "Completed")
            {
                active = Enumerable.Empty<CustomerReservationViewModel>();
                history = history.Where(r => r.Status == "Converted");
            }
            else if (normalizedTab == "Expired")
            {
                active = Enumerable.Empty<CustomerReservationViewModel>();
                history = history.Where(r => r.Status == "Expired");
            }
            else if (normalizedTab == "Cancelled")
            {
                active = Enumerable.Empty<CustomerReservationViewModel>();
                history = history.Where(r => r.Status == "Cancelled");
            }

            active = normalizedSort switch
            {
                "oldest" => active.OrderBy(r => r.ReservationDate),
                "expiring" => active.OrderBy(r => r.ExpiresAt),
                _ => active.OrderByDescending(r => r.ReservationDate)
            };

            history = normalizedSort switch
            {
                "oldest" => history.OrderBy(r => r.ReservationDate),
                _ => history.OrderByDescending(r => r.ReservationDate)
            };

            vm.ActiveReservations = active.ToList();
            vm.HistoryReservations = history.ToList();

            return View(vm);
        }

        // POST: /Reservation/Create - galing sa Details page
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int inventoryId, int quantity)
        {
            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId == null)
            {
                TempData["Error"] = "Please log in first to make a reservation.";
                return RedirectToAction("Login", "Account");
            }

            if (quantity <= 0)
            {
                TempData["Error"] = "Invalid quantity.";
                return RedirectToAction("Index", "Shop");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            var inventory = await _context.ShoeInventories
                .FirstOrDefaultAsync(i => i.inventory_id == inventoryId);

            if (inventory == null)
            {
                TempData["Error"] = "Selected size is no longer available.";
                return RedirectToAction("Index", "Shop");
            }

            if (await GetAvailableQuantityAsync(inventoryId) < quantity)
            {
                TempData["Error"] = "Not enough stock for the selected size.";
                return RedirectToAction("Details", "Shop", new { id = inventory.shoe_id });
            }

            var reservation = new Reservation
            {
                customer_id = customerId.Value,
                inventory_id = inventoryId,
                quantity = quantity,
                status = "Pending",
                reservation_date = DateTime.Now,
                expires_at = DateTime.Now.AddHours(72)
            };

            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Success"] = "Reservation submitted! Please wait for admin approval.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Reservation/Cancel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            await UpdateExpiredReservationsAsync();

            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId == null)
                return RedirectToAction("Login", "Account");

            var reservation = await _context.Reservations
                .Include(r => r.ShoeInventory)
                .FirstOrDefaultAsync(r => r.reservation_id == id && r.customer_id == customerId);

            if (reservation == null)
                return NotFound();

            if (reservation.status != "Pending" && reservation.status != "Approved")
            {
                TempData["Error"] = "This reservation can no longer be cancelled.";
                return RedirectToAction(nameof(Index));
            }

            if (reservation.status == "Approved" && reservation.ShoeInventory != null)
                reservation.ShoeInventory.quantity_in_stock += reservation.quantity;

            reservation.status = "Cancelled";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Reservation cancelled.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int id)
        {
            await UpdateExpiredReservationsAsync();

            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId == null)
                return RedirectToAction("Login", "Account");

            var reservation = await _context.Reservations
                .Include(r => r.ShoeInventory)
                    .ThenInclude(si => si!.ShoeCatalog)
                        .ThenInclude(sc => sc!.Images)
                .FirstOrDefaultAsync(r => r.reservation_id == id && r.customer_id == customerId);

            if (reservation == null)
                return NotFound();

            var catalog = reservation.ShoeInventory?.ShoeCatalog;

            var images = catalog?.Images?
                .OrderBy(img => img.DisplayOrder)
                .Select(img => img.FilePath)
                .ToList() ?? new List<string>();

            if (!images.Any())
                images.Add(string.IsNullOrEmpty(catalog?.image_path) ? "/images/no-image.png" : catalog!.image_path!);

            var vm = new CustomerReservationDetailsViewModel
            {
                ReservationId = reservation.reservation_id,
                ShoeName = catalog?.model_name ?? "Unknown",
                Brand = catalog?.brand ?? "Unknown",
                Category = catalog?.category ?? "Unknown",
                Color = catalog?.color,
                ImagePaths = images,
                Size = reservation.ShoeInventory?.size ?? 0,
                Quantity = reservation.quantity,
                Price = catalog == null
                    ? 0
                    : PromotionHelper.GetEffectivePrice(catalog.default_price, PromotionHelper.GetCurrentPromotion(catalog)),
                Status = reservation.status,
                ReservationDate = reservation.reservation_date,
                ExpiresAt = reservation.expires_at,
                ApprovedAt = reservation.approved_at,
                ConvertedOrderId = reservation.converted_order_id
            };

            return View(vm);
        }

        public async Task<IActionResult> Checkout(int id)
        {
            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId == null)
                return RedirectToAction("Login", "Account");

            var reservation = await _context.Reservations
                .Include(r => r.ShoeInventory)
                    .ThenInclude(si => si!.ShoeCatalog)
                .FirstOrDefaultAsync(r => r.reservation_id == id && r.customer_id == customerId);

            if (reservation == null)
                return NotFound();

            if (reservation.status != "Approved")
            {
                TempData["Error"] = "Only approved reservations can proceed to order.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (reservation.converted_order_id.HasValue)
            {
                TempData["Error"] = "This reservation has already been converted to an order.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (reservation.ShoeInventory == null || reservation.ShoeInventory.ShoeCatalog == null)
            {
                TempData["Error"] = "This reservation contains invalid product information.";
                return RedirectToAction(nameof(Details), new { id });
            }

            return RedirectToAction("Delivery", "Checkout", new { reservationId = id });
        }
    }
}