using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOrderSystem.Data;
using SmartOrderSystem.ViewModels;

namespace SmartOrderSystem.Controllers
{
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrderController(ApplicationDbContext context)
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