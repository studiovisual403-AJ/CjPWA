using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOrderSystem.Data;
using SmartOrderSystem.Helpers;
using SmartOrderSystem.ViewModels;
using MySqlConnector;
using System.Data;

namespace SmartOrderSystem.Controllers
{
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrderController(ApplicationDbContext context)
        {
            _context = context;
        }

        private static bool IsRetryableLockFailure(Exception exception)
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (current is MySqlException mysqlException &&
                    (mysqlException.Number == 1213 || mysqlException.Number == 1205))
                    return true;
            }

            return false;
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

        // GET: /Order - "My Orders" page
        public async Task<IActionResult> Index(string? tab, string? sort)
        {
            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId == null)
                return RedirectToAction("Login", "Account");

            var normalizedTab = string.IsNullOrWhiteSpace(tab) ? "All" : tab;
            var normalizedSort = string.IsNullOrWhiteSpace(sort) ? "recent" : sort;

            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.ShoeInventory)
                        .ThenInclude(si => si!.ShoeCatalog)
                .Where(o => o.customer_id == customerId)
                .ToListAsync();

            var vm = new CustomerOrderListViewModel
            {
                CurrentTab = normalizedTab,
                CurrentSort = normalizedSort,
                AllCount = orders.Count,
                PendingCount = orders.Count(o => o.status == "Pending"),
                ConfirmedCount = orders.Count(o => o.status == "Confirmed"),
                PreparingCount = orders.Count(o => o.status == "Preparing"),
                InTransitCount = orders.Count(o => o.status == "In Transit"),
                DeliveredCount = orders.Count(o => o.status == "Delivered"),
                CompletedCount = orders.Count(o => o.status == "Completed"),
                CancelledCount = orders.Count(o => o.status == "Cancelled")
            };

            var filtered = normalizedTab == "All"
                ? orders
                : orders.Where(o => o.status == normalizedTab).ToList();

            filtered = normalizedSort switch
            {
                "oldest" => filtered.OrderBy(o => o.order_date).ToList(),
                _ => filtered.OrderByDescending(o => o.order_date).ToList()
            };

            vm.Orders = filtered.Select(o =>
            {
                var firstItem = o.OrderItems.FirstOrDefault();
                var catalog = firstItem?.ShoeInventory?.ShoeCatalog;

                return new CustomerOrderCardViewModel
                {
                    OrderId = o.order_id,
                    OrderDate = o.order_date,
                    TotalAmount = o.total_amount,
                    Status = o.status,
                    FirstItemName = catalog?.model_name ?? "Unknown",
                    FirstItemBrand = catalog?.brand ?? "Unknown",
                    FirstItemImagePath = catalog?.image_path,
                    FirstItemSize = firstItem?.ShoeInventory?.size,
                    FirstItemQuantity = firstItem?.quantity ?? 0,
                    TotalDistinctItems = o.OrderItems.Count
                };
            }).ToList();

            return View(vm);
        }

        // POST: /Order/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, string? reason, string? otherReason)
        {
            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId == null)
                return RedirectToAction("Login", "Account");

            var finalReason = (reason == "Other" ? otherReason : reason)?.Trim();
            if (string.IsNullOrEmpty(finalReason))
            {
                TempData["OrderError"] = "Select a reason for cancelling.";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (finalReason.Length > 200)
                finalReason = finalReason[..200];

            var customerIdValue = customerId.Value;

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                var order = await _context.Orders
                    .FromSqlInterpolated($"SELECT * FROM orders WHERE order_id = {id} AND customer_id = {customerIdValue} FOR UPDATE")
                    .FirstOrDefaultAsync();

                if (order == null)
                    return NotFound();

                await _context.Entry(order).Collection(o => o.OrderItems).LoadAsync();

                var currentStatus = order.status?.Trim();
                var isPending = string.Equals(currentStatus, "Pending", StringComparison.OrdinalIgnoreCase);
                var isConfirmed = string.Equals(currentStatus, "Confirmed", StringComparison.OrdinalIgnoreCase);
                if (!isPending && !isConfirmed)
                {
                    TempData["OrderError"] = $"This order can't be cancelled anymore. Current status: {order.status}.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                // Stock is deducted for confirmed orders, so only those orders give it back.
                if (isConfirmed)
                {
                    var refundedQuantities = order.OrderItems
                        .GroupBy(item => item.inventory_id)
                        .ToDictionary(group => group.Key, group => group.Sum(item => item.quantity));

                    var inventoryIds = refundedQuantities.Keys.OrderBy(x => x).ToList();
                    var lockedInventory = await _context.LockInventoryRowsAsync(inventoryIds);

                    foreach (var refunded in refundedQuantities)
                    {
                        if (lockedInventory.TryGetValue(refunded.Key, out var shoe))
                            shoe.quantity_in_stock += refunded.Value;
                    }
                }

                order.status = "Cancelled";
                order.shipping_status = "Cancelled";
                order.cancellation_reason = finalReason;
                order.updated_at = DateTime.Now;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex) when (IsRetryableLockFailure(ex))
            {
                await transaction.RollbackAsync();
                TempData["OrderError"] = "Your order couldn't be cancelled right now. Try again.";
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["OrderSuccess"] = "Your order has been cancelled.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: /Order/Details/5
        public async Task<IActionResult> Details(int id, string? tab, string? sort)
        {
            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId == null)
                return RedirectToAction("Login", "Account");

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.ShoeInventory)
                        .ThenInclude(si => si!.ShoeCatalog)
                .FirstOrDefaultAsync(o => o.order_id == id && o.customer_id == customerId);

            if (order == null)
                return NotFound();

            // Fetch all of this customer's ratings for the shoes in this order in ONE query,
            // instead of querying per item (avoids N+1 queries).
            var shoeIds = order.OrderItems
                .Select(i => i.ShoeInventory?.ShoeCatalog?.shoe_id ?? 0)
                .Distinct()
                .ToList();

            var ratingsByShoeId = await _context.ProductRatings
                .Where(r => shoeIds.Contains(r.shoe_id) && r.customer_id == customerId.Value)
                .ToDictionaryAsync(r => r.shoe_id, r => (int?)r.rating);

            var vm = new CustomerOrderDetailsViewModel
            {
                OrderId = order.order_id,
                OrderDate = order.order_date,
                UpdatedAt = order.updated_at,
                Status = order.status,
                PaymentMethod = order.payment_method,
                PaymentStatus = order.payment_status,
                Courier = order.courier,
                TrackingNumber = order.tracking_number,
                CancellationReason = order.cancellation_reason,
                TotalAmount = order.total_amount,
                ShippingFee = order.shipping_fee,
                ReturnTab = string.IsNullOrWhiteSpace(tab) ? "All" : tab,
                ReturnSort = string.IsNullOrWhiteSpace(sort) ? "recent" : sort,
                Items = order.OrderItems.Select(i =>
                {
                    var shoeId = i.ShoeInventory?.ShoeCatalog?.shoe_id ?? 0;

                    return new CustomerOrderItemViewModel
                    {
                        ShoeId = shoeId,
                        ShoeName = i.ShoeInventory?.ShoeCatalog?.model_name ?? "Unknown",
                        Brand = i.ShoeInventory?.ShoeCatalog?.brand ?? "Unknown",
                        Category = i.ShoeInventory?.ShoeCatalog?.category ?? "Unknown",
                        ImagePath = i.ShoeInventory?.ShoeCatalog?.image_path,
                        Size = i.ShoeInventory?.size ?? 0,
                        Quantity = i.quantity,
                        ItemPrice = i.item_price,
                        ExistingRating = ratingsByShoeId.TryGetValue(shoeId, out var rating) ? rating : null
                    };
                }).ToList()
            };

            return View(vm);
        }
    }
}