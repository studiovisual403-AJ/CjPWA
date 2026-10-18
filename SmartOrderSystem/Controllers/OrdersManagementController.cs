using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOrderSystem.Data;
using SmartOrderSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data;
using MySqlConnector;
using SmartOrderSystem.Services;
using SmartOrderSystem.Helpers;

namespace SmartOrderSystem.Controllers
{
    public class OrdersManagementController : AdminBaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public OrdersManagementController(ApplicationDbContext context, INotificationService notificationService) : base(context)
        {
            _context = context;
            _notificationService = notificationService;
        }

        private IQueryable<Order> GetOrderQuery()
        {
            return _context.Orders.Include(o => o.Customer).AsQueryable();
        }

        private async Task<Order?> GetOrderWithItemsAsync(int orderId)
        {
            return await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.order_id == orderId);
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

        private async Task<Order?> GetOrderWithDetailsAsync(int id)
        {
            return await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.ShippingAddress)
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.ShoeInventory)
                        .ThenInclude(si => si.ShoeCatalog)
                .FirstOrDefaultAsync(m => m.order_id == id);
        }

        private static string? FormatShippingAddress(CustomerAddress? address)
        {
            if (address == null)
                return null;

            var parts = new[]
            {
                address.recipient_name,
                address.contact_number,
                address.street_address,
                address.barangay,
                address.city,
                address.province,
                address.postal_code,
                address.landmark
            };

            return string.Join(", ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        // 1. INDEX ACTION: LIST OF ORDERS WITH DYNAMIC COUNTERS
        public async Task<IActionResult> Index(string searchString, string statusFilter, int pageNumber = 1)
        {
            ViewBag.PendingCount = await _context.Orders.CountAsync(o => o.status == "Pending");
            ViewBag.ConfirmedCount = await _context.Orders.CountAsync(o => o.status == "Confirmed");
            ViewBag.PreparingCount = await _context.Orders.CountAsync(o => o.status == "Preparing");
            ViewBag.InTransitCount = await _context.Orders.CountAsync(o => o.status == "In Transit");
            ViewBag.DeliveredCount = await _context.Orders.CountAsync(o => o.status == "Delivered");
            ViewBag.CompletedCount = await _context.Orders.CountAsync(o => o.status == "Completed");
            ViewBag.CancelledCount = await _context.Orders.CountAsync(o => o.status == "Cancelled");

            var query = GetOrderQuery();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(o => o.order_id.ToString().Contains(searchString) ||
                                         o.Customer.full_name.Contains(searchString));
            }

            if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "All")
            {
                query = query.Where(o => o.status == statusFilter);
            }

            ViewBag.CurrentSearch = searchString;
            ViewBag.CurrentStatus = statusFilter ?? "All";

            int pageSize = 10;
            var totalOrders = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalOrders / pageSize));
            pageNumber = Math.Clamp(pageNumber, 1, totalPages);
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalOrders = totalOrders;
            ViewBag.PageIndex = pageNumber;
            ViewBag.HasPreviousPage = pageNumber > 1;
            ViewBag.HasNextPage = pageNumber < ViewBag.TotalPages;

            var orders = await query.OrderByDescending(o => o.order_date)
                                    .Skip((pageNumber - 1) * pageSize)
                                    .Take(pageSize)
                                    .ToListAsync();

            return View(orders);
        }

        // 2. DETAILS ACTION
        public async Task<IActionResult> Details(int id)
        {
            var order = await GetOrderWithDetailsAsync(id);

            if (order == null) return NotFound();

            order.shipping_address = FormatShippingAddress(order.ShippingAddress);

            return View(order);
        }

        // 3. CONFIRM ORDER: Validate stock -> Ibawas stock -> Pending -> Confirmed
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmOrder(int orderId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                var order = await _context.Orders
                    .FromSqlInterpolated($"SELECT * FROM orders WHERE order_id = {orderId} FOR UPDATE")
                    .FirstOrDefaultAsync();

                if (order == null)
                    return Json(new { success = false, message = "Order not found." });

                await _context.Entry(order).Collection(o => o.OrderItems).LoadAsync();

                if (!string.Equals(order.status?.Trim(), "Pending", StringComparison.OrdinalIgnoreCase))
                    return Json(new { success = false, message = "This order can no longer be confirmed." });

                if (order.OrderItems.Count == 0 || order.OrderItems.Any(item => item.quantity <= 0))
                    return Json(new { success = false, message = "The order contains invalid item quantities." });

                var requestedQuantities = order.OrderItems
                    .GroupBy(item => item.inventory_id)
                    .ToDictionary(group => group.Key, group => group.Sum(item => item.quantity));

                var inventoryIds = requestedQuantities.Keys.OrderBy(x => x).ToList();
                var lockedInventory = await _context.LockInventoryRowsAsync(inventoryIds);

                foreach (var requested in requestedQuantities)
                {
                    if (!lockedInventory.TryGetValue(requested.Key, out var shoe) || shoe.quantity_in_stock < requested.Value)
                    {
                        var available = lockedInventory.TryGetValue(requested.Key, out var current) ? current.quantity_in_stock : 0;
                        return Json(new
                        {
                            success = false,
                            message = $"Insufficient stock for one or more items. (Available: {available}, Required: {requested.Value})"
                        });
                    }
                }

                foreach (var requested in requestedQuantities)
                {
                    lockedInventory[requested.Key].quantity_in_stock -= requested.Value;
                }

                order.status = "Confirmed";
                order.shipping_status = "Confirmed";
                order.updated_at = DateTime.Now;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _notificationService.NotifyCustomerAsync(
                    order.customer_id,
                    "Confirmed",
                    "Order Confirmed",
                    $"Your order #ORD-{order.order_id:D4} has been confirmed and is now being processed.",
                    orderId: order.order_id);

                return Json(new { success = true, message = "Order confirmed successfully. Stock was deducted.", newStatus = "Confirmed" });
            }
            catch (Exception ex) when (IsRetryableLockFailure(ex))
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Database contention prevented confirming this order. Please retry." });
            }
        }

        // 4. CANCEL ORDER: with reason, ibabalik ang stock kung na-deduct na
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int orderId, string reason)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                var order = await _context.Orders
                    .FromSqlInterpolated($"SELECT * FROM orders WHERE order_id = {orderId} FOR UPDATE")
                    .FirstOrDefaultAsync();

                if (order == null)
                    return Json(new { success = false, message = "Order not found." });

                await _context.Entry(order).Collection(o => o.OrderItems).LoadAsync();

                if (string.IsNullOrWhiteSpace(reason))
                    return Json(new { success = false, message = "A cancellation reason is required." });

                var currentStatus = order.status?.Trim().ToLower();
                var cancellableStatuses = new[] { "pending", "confirmed", "preparing" };

                if (!cancellableStatuses.Contains(currentStatus))
                    return Json(new { success = false, message = "This order can no longer be cancelled because it has already been shipped or completed." });

                if (currentStatus == "confirmed" || currentStatus == "preparing")
                {
                    var refundedQuantities = order.OrderItems
                        .GroupBy(item => item.inventory_id)
                        .ToDictionary(group => group.Key, group => group.Sum(item => item.quantity));

                    var inventoryIds = refundedQuantities.Keys.OrderBy(x => x).ToList();
                    var lockedInventory = await _context.LockInventoryRowsAsync(inventoryIds);

                    foreach (var refunded in refundedQuantities)
                    {
                        if (lockedInventory.TryGetValue(refunded.Key, out var shoe))
                        {
                            shoe.quantity_in_stock += refunded.Value;
                        }
                    }
                }

                order.status = "Cancelled";
                order.shipping_status = "Cancelled";
                order.cancellation_reason = reason;
                order.updated_at = DateTime.Now;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _notificationService.NotifyCustomerAsync(
                    order.customer_id,
                    "Cancelled",
                    "Order Cancelled",
                    $"Your order #ORD-{order.order_id:D4} was cancelled. Reason: {reason}",
                    orderId: order.order_id);

                return Json(new { success = true, message = "The order was cancelled successfully." });
            }
            catch (Exception ex) when (IsRetryableLockFailure(ex))
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Database contention prevented cancelling this order. Please retry." });
            }
        }

        // 5. MARK AS PAID (COD): Delivered -> Completed
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsPaid(int orderId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            var order = await _context.Orders
                .FromSqlInterpolated($"SELECT * FROM orders WHERE order_id = {orderId} FOR UPDATE")
                .FirstOrDefaultAsync();
            if (order == null)
                return Json(new { success = false, message = "Order not found." });

            if (order.status?.Trim().ToLower() != "delivered")
                return Json(new { success = false, message = "This order cannot be marked as paid yet." });

            if (!string.Equals(order.payment_method?.Trim(), "Cash on Delivery", StringComparison.OrdinalIgnoreCase))
                return Json(new { success = false, message = "Only Cash on Delivery orders can be marked as paid here." });

            order.payment_status = "Paid";
            order.status = "Completed";
            order.shipping_status = "Completed";
            order.updated_at = DateTime.Now;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Json(new { success = true, message = "The order was marked as paid and completed." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NotifyCustomer(int orderId)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.order_id == orderId);
            if (order == null)
                return Json(new { success = false, message = "Order not found." });

            await _notificationService.NotifyCustomerAsync(
                order.customer_id,
                "OrderUpdate",
                "Order Update",
                $"Your order #ORD-{order.order_id:D4} is currently {order.status}.",
                orderId: order.order_id);

            return Json(new { success = true, message = "Customer notification sent." });
        }

        // 6. PIPELINE: Preparing -> In Transit -> Delivered (courier assignment)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessStatus(int orderId, string nextStatus, string courier, string trackingNumber)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            var order = await _context.Orders
                .FromSqlInterpolated($"SELECT * FROM orders WHERE order_id = {orderId} FOR UPDATE")
                .FirstOrDefaultAsync();
            if (order == null) return NotFound();

            var normalizedCurrentStatus = order.status?.Trim() ?? string.Empty;
            var normalizedNextStatus = nextStatus?.Trim() ?? string.Empty;
            var allowedTransitions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Confirmed"] = new[] { "Preparing" },
                ["Preparing"] = new[] { "In Transit" },
                ["In Transit"] = new[] { "Delivered" }
            };

            if (!allowedTransitions.TryGetValue(normalizedCurrentStatus, out var allowedStatuses) ||
            !allowedStatuses.Contains(normalizedNextStatus, StringComparer.OrdinalIgnoreCase))
            {
                TempData["Error"] = "That order status transition is not allowed.";
                return RedirectToAction(nameof(Details), new { id = orderId });
            }

            var trimmedCourier = courier?.Trim();
            var trimmedTracking = trackingNumber?.Trim();

            if (trimmedCourier?.Length > 100 || trimmedTracking?.Length > 100)
            {
                TempData["Error"] = "Courier and tracking number must be 100 characters or fewer.";
                return RedirectToAction(nameof(Details), new { id = orderId });
            }

            // Courier at tracking number ay REQUIRED bago mag In Transit
            if (string.Equals(normalizedNextStatus, "In Transit", StringComparison.OrdinalIgnoreCase))
            {
                var finalCourier = !string.IsNullOrWhiteSpace(trimmedCourier) ? trimmedCourier : order.courier?.Trim();
                var finalTracking = !string.IsNullOrWhiteSpace(trimmedTracking) ? trimmedTracking : order.tracking_number?.Trim();

                if (string.IsNullOrWhiteSpace(finalCourier) || string.IsNullOrWhiteSpace(finalTracking))
                {
                    TempData["Error"] = "Courier and tracking number are required before marking the order as In Transit.";
                    return RedirectToAction(nameof(Details), new { id = orderId });
                }
            }

            if (!string.IsNullOrWhiteSpace(trimmedCourier)) order.courier = trimmedCourier;
            if (!string.IsNullOrWhiteSpace(trimmedTracking)) order.tracking_number = trimmedTracking;

            order.status = allowedStatuses.First(status =>
            string.Equals(status, normalizedNextStatus, StringComparison.OrdinalIgnoreCase));
            order.shipping_status = order.status;
            order.updated_at = DateTime.Now;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var (notifTitle, notifMessage) = order.status switch
            {
                "Preparing" => ("Order Being Prepared", $"Your order #ORD-{order.order_id:D4} is now being prepared for shipment."),
                "In Transit" => ("Order In Transit", $"Your order #ORD-{order.order_id:D4} is on its way to you."),
                "Delivered" => ("Order Delivered", $"Your order #ORD-{order.order_id:D4} has been delivered. Thank you for shopping with us!"),
                _ => ("Order Update", $"Your order #ORD-{order.order_id:D4} has an update.")
            };

            await _notificationService.NotifyCustomerAsync(order.customer_id, order.status!, notifTitle, notifMessage, orderId: order.order_id);

            // POP-UP: success toast sa admin
            TempData["Success"] = $"Order #ORD-{order.order_id:D4} is now {order.status}. Customer has been notified.";

            return RedirectToAction(nameof(Details), new { id = orderId });
        }
    }
}
