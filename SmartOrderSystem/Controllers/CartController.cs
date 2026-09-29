using SmartOrderSystem.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOrderSystem.Data;
using SmartOrderSystem.Models;
using SmartOrderSystem.Models.ViewModels;

namespace SmartOrderSystem.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Kunin yung logged-in customer's ID gamit yung email na naka-store sa session
        private int? GetCurrentCustomerId()
        {
            var email = HttpContext.Session.GetString("CustomerUser");
            if (string.IsNullOrEmpty(email)) return null;

            var customer = _context.Customers.FirstOrDefault(c => c.email == email);
            return customer?.customer_id;
        }

        public async Task<IActionResult> Index()
        {
                        var customerId = GetCurrentCustomerId();
            if (customerId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Cleanup: remove any corrupt cart items with invalid quantity
            var corruptItems = _context.CartItems.Where(c => c.CustomerId == customerId && c.Quantity <= 0);
            if (await corruptItems.AnyAsync())
            {
                _context.CartItems.RemoveRange(corruptItems);
                await _context.SaveChangesAsync();
            }

            var cartItems = await _context.CartItems
                .Include(c => c.Shoe)
                    .ThenInclude(s => s.Promotions)
                .Where(c => c.CustomerId == customerId)
                .ToListAsync();

            var vm = new CartViewModel();

            foreach (var item in cartItems)
            {
                vm.Items.Add(new CartItemDisplay
                {
                    CartItemId = item.CartItemId,
                    ShoeId = item.ShoeId,
                    Name = item.Shoe.model_name,
                    Variant = item.Shoe.brand,
                    Size = item.Size,
                    Color = item.Color,
                    ImageUrl = item.Shoe.image_path,
                    Quantity = item.Quantity,
                    Price = PromotionHelper.GetEffectivePrice(item.Shoe.default_price, PromotionHelper.GetCurrentPromotion(item.Shoe))
                });
            }

            vm.Subtotal = vm.Items.Sum(i => i.LineTotal);
            vm.ShippingFee = vm.QualifiesForFreeShipping ? 0 : ShippingHelper.ShippingFee;

            return View(vm);
        }

                [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(int inventoryId, int quantity = 1)
        {
            if (quantity <= 0)
            {
                return Json(new { success = false, message = "Quantity must be at least 1." });
            }

            var customerId = GetCurrentCustomerId();
    if (customerId == null)
    {
        return Json(new { success = false, redirectToLogin = true });
    }

    var inventory = await _context.ShoeInventories // TODO: i-verify pangalan ng DbSet
        .Include(i => i.ShoeCatalog)
        .FirstOrDefaultAsync(i => i.inventory_id == inventoryId);

    if (inventory == null || inventory.ShoeCatalog == null)
        return Json(new { success = false, message = "Item not found." });

    if (inventory.quantity_in_stock < quantity)
        return Json(new { success = false, message = "Not enough stock available." });

    var shoe = inventory.ShoeCatalog;
    var sizeLabel = inventory.size.ToString("0.#");
    var color = shoe.color ?? "";

    var existing = await _context.CartItems.FirstOrDefaultAsync(c =>
        c.CustomerId == customerId && c.ShoeId == shoe.shoe_id && c.Size == sizeLabel && c.Color == color);

    var requestedTotal = (existing?.Quantity ?? 0) + quantity;
    if (requestedTotal > inventory.quantity_in_stock)
        return Json(new { success = false, message = $"Only {inventory.quantity_in_stock} item(s) are available for this size." });

    if (existing != null)
    {
        existing.Quantity = requestedTotal;
    }
    else
    {
        _context.CartItems.Add(new CartItem
        {
            CustomerId = customerId.Value,
            ShoeId = shoe.shoe_id,
            Size = sizeLabel,
            Color = color,
            Quantity = quantity,
            PriceAtAddTime = PromotionHelper.GetEffectivePrice(shoe.default_price, PromotionHelper.GetCurrentPromotion(shoe)),
            CreatedAt = DateTime.Now
        });
    }

    await _context.SaveChangesAsync();

    var cartCount = await _context.CartItems
        .Where(c => c.CustomerId == customerId)
        .SumAsync(c => c.Quantity);

    return Json(new { success = true, cartCount });
}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateQuantity(int cartItemId, int quantity)
        {
            var customerId = GetCurrentCustomerId();
            if (customerId == null) return RedirectToAction("Login", "Account");

            var item = await _context.CartItems.FirstOrDefaultAsync(c => c.CartItemId == cartItemId && c.CustomerId == customerId);
            if (item == null) return NotFound();

            var requestedQuantity = quantity < 1 ? 1 : quantity;
            if (!decimal.TryParse(item.Size, out var size)) return RedirectToAction("Index");

            var available = await _context.ShoeInventories
                .Where(i => i.shoe_id == item.ShoeId && i.size == size)
                .Select(i => (int?)i.quantity_in_stock)
                .FirstOrDefaultAsync() ?? 0;
            if (requestedQuantity > available)
            {
                TempData["Error"] = $"Only {available} item(s) are available for this size.";
                return RedirectToAction("Index");
            }

            item.Quantity = requestedQuantity;
            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveItem(int cartItemId)
        {
            var customerId = GetCurrentCustomerId();
            if (customerId == null) return RedirectToAction("Login", "Account");

            var item = await _context.CartItems.FirstOrDefaultAsync(c => c.CartItemId == cartItemId && c.CustomerId == customerId);
            if (item != null)
            {
                _context.CartItems.Remove(item);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearCart()
        {
            var customerId = GetCurrentCustomerId();
            if (customerId == null) return RedirectToAction("Login", "Account");

            var items = _context.CartItems.Where(c => c.CustomerId == customerId);
            _context.CartItems.RemoveRange(items);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }
    }
}