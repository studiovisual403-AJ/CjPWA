using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOrderSystem.Data;
using SmartOrderSystem.Helpers;
using SmartOrderSystem.Models;
using SmartOrderSystem.Services;
using SmartOrderSystem.ViewModels;
using System.Data;

namespace SmartOrderSystem.Controllers
{
    public class ReservationManagementController : AdminBaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public ReservationManagementController(ApplicationDbContext context, INotificationService notificationService) : base(context)
        {
            _context = context;
            _notificationService = notificationService;
        }

        private IQueryable<Reservation> GetReservationQuery()
        {
            return _context.Reservations
                .Include(r => r.Customer)
                .Include(r => r.ShoeInventory)
                    .ThenInclude(si => si.ShoeCatalog)
                .AsQueryable();
        }

        private async Task<Reservation?> GetReservationByIdAsync(int id)
        {
            return await GetReservationQuery()
                .FirstOrDefaultAsync(r => r.reservation_id == id);
        }

        private async Task UpdateExpiredReservations()
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            var expiredIds = await _context.Reservations
                .Where(r => r.status == "Pending"
                         && r.expires_at.HasValue
                         && r.expires_at <= DateTime.Now)
                .Select(r => r.reservation_id)
                .ToListAsync();

            foreach (var id in expiredIds.OrderBy(x => x))
            {
                var reservation = await _context.LockReservationAsync(id);
                if (reservation?.status == "Pending" && reservation.expires_at <= DateTime.Now)
                    reservation.status = "Expired";
            }

            if (expiredIds.Count == 0)
            {
                await transaction.CommitAsync();
                return;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        public async Task<IActionResult> Index(
            string? searchString,
            string? statusFilter,
            DateTime? reservationDate)
        {
            await UpdateExpiredReservations();

            var query = GetReservationQuery();
            var normalizedStatus = string.IsNullOrWhiteSpace(statusFilter) ? "All" : statusFilter.Trim();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var search = searchString.Trim();
                query = query.Where(r =>
                    r.reservation_id.ToString().Contains(search) ||
                    (r.Customer != null && r.Customer.full_name.Contains(search)) ||
                    (r.ShoeInventory != null && r.ShoeInventory.ShoeCatalog != null &&
                     r.ShoeInventory.ShoeCatalog.model_name.Contains(search)));
            }

            if (!string.Equals(normalizedStatus, "All", StringComparison.OrdinalIgnoreCase))
                query = query.Where(r => r.status == normalizedStatus);

            if (reservationDate.HasValue)
            {
                var start = reservationDate.Value.Date;
                var end = start.AddDays(1);
                query = query.Where(r => r.reservation_date >= start && r.reservation_date < end);
            }

            var reservations = await query
                .OrderByDescending(r => r.reservation_date)
                .ToListAsync();

            ViewBag.PendingCount = reservations.Count(r => r.status == "Pending");
            ViewBag.ApprovedCount = reservations.Count(r => r.status == "Approved");
            ViewBag.ConvertedCount = reservations.Count(r => r.status == "Converted");
            ViewBag.ExpiredCount = reservations.Count(r => r.status == "Expired");
            ViewBag.CurrentStatus = normalizedStatus;
            ViewBag.CurrentSearch = searchString?.Trim() ?? string.Empty;
            ViewBag.CurrentReservationDate = reservationDate?.ToString("yyyy-MM-dd") ?? string.Empty;
            ViewBag.PageIndex = 1;
            ViewBag.TotalPages = 1;
            ViewBag.TotalCount = reservations.Count;

            return View(reservations);
        }

        public async Task<IActionResult> Details(int id)
        {
            await UpdateExpiredReservations();

            var reservation = await GetReservationByIdAsync(id);
            if (reservation == null)
                return NotFound();

            var viewModel = new ReservationDetailsViewModel
            {
                ReservationId = reservation.reservation_id,
                CustomerName = reservation.Customer?.full_name ?? "N/A",
                CustomerEmail = reservation.Customer?.email ?? "N/A",
                CustomerPhone = reservation.Customer?.contact_number ?? "N/A",
                ImagePath = reservation.ShoeInventory?.ShoeCatalog?.image_path ?? "/images/no-image.png",
                ShoeName = reservation.ShoeInventory?.ShoeCatalog?.model_name ?? "Unknown",
                Brand = reservation.ShoeInventory?.ShoeCatalog?.brand ?? "Unknown",
                Category = reservation.ShoeInventory?.ShoeCatalog?.category ?? "Unknown",
                Color = reservation.ShoeInventory?.ShoeCatalog?.color ?? "N/A",
                Price = reservation.ShoeInventory?.ShoeCatalog?.default_price ?? 0,
                Quantity = reservation.quantity,
                ReservationDate = reservation.reservation_date,
                ExpiresAt = reservation.expires_at,
                Status = reservation.status
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            return await UpdateStatus(id, "Approved");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            bool approving = false;

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

            try
            {
                // 1. Lock ang reservation row muna (iwas double-approve kapag dalawang admin ang sabay)
                var reservation = await _context.LockReservationAsync(id);
                if (reservation == null)
                    return NotFound();

                approving = string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase)
                            && reservation.status == "Pending";
                var cancelling = string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase)
                                 && (reservation.status == "Pending" || reservation.status == "Approved");

                if (!approving && !cancelling)
                    return BadRequest("That reservation status update is not allowed.");

                if (approving && reservation.expires_at.HasValue && reservation.expires_at.Value <= DateTime.Now)
                    return BadRequest("This reservation has expired.");

                // 2. Lock ang inventory row
                var lockedInventory = await _context.LockInventoryRowsAsync(new[] { reservation.inventory_id });
                lockedInventory.TryGetValue(reservation.inventory_id, out var inventory);

                if (approving)
                {
                    if (reservation.quantity <= 0 || inventory == null)
                        return BadRequest("The reservation has invalid inventory or quantity data.");

                    var pendingReservationQuantity = await _context.GetPendingReservationQuantityAsync(
                        reservation.inventory_id, DateTime.Now);
                    if (reservation.quantity > inventory.quantity_in_stock ||
                        pendingReservationQuantity > inventory.quantity_in_stock)
                        return BadRequest($"Insufficient stock. Available: {Math.Max(0, inventory.quantity_in_stock)}, Required: {reservation.quantity}");

                    inventory.quantity_in_stock -= reservation.quantity;
                    reservation.status = "Approved";
                    reservation.approved_at = DateTime.Now;
                    reservation.approved_by = await GetCurrentAdminIdAsync();
                }
                else
                {
                    if (reservation.status == "Approved" && inventory != null)
                        inventory.quantity_in_stock += reservation.quantity;

                    reservation.status = "Cancelled";
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync(); // dito nare-release ang locks
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Could not update the reservation right now. Please try again.";
                return RedirectToAction(nameof(Index));
            }

            if (approving)
            {
                var info = await _context.Reservations
                    .AsNoTracking()
                    .Where(r => r.reservation_id == id)
                    .Select(r => new { r.customer_id, ShoeName = r.ShoeInventory!.ShoeCatalog!.model_name })
                    .FirstOrDefaultAsync();

                if (info != null)
                {
                    await _notificationService.NotifyCustomerAsync(
                        info.customer_id,
                        "ReservationConfirmed",
                        "Reservation Confirmed",
                        $"Your reservation for {info.ShoeName ?? "your reserved item"} has been confirmed.");
                }
            }

            TempData["Success"] = "Reservation status updated successfully.";
            return RedirectToAction(nameof(Index));
        }
        private async Task<int?> GetCurrentAdminIdAsync()
        {
            var email = HttpContext.Session.GetString("AdminUser");
            if (string.IsNullOrWhiteSpace(email))
                return null;

            return await _context.Admins
                .Where(a => a.email == email)
                .Select(a => (int?)a.admin_id)
                .FirstOrDefaultAsync();
        }
    }
}
