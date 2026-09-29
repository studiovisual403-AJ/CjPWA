using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOrderSystem.Data;
using SmartOrderSystem.Models;
using SmartOrderSystem.Helpers;

namespace SmartOrderSystem.Controllers
{
    public class ShopController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ShopController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. Homepage / Landing Page
        public async Task<IActionResult> Index()
        {
            // Rating stats per shoe
            var ratingStats = await _context.ProductRatings
                .GroupBy(r => r.shoe_id)
                .Select(g => new
                {
                    ShoeId = g.Key,
                    AvgRating = g.Average(x => x.rating),
                    RatingCount = g.Count()
                })
                .ToListAsync();

            // Sold count: Completed orders lang
            var completedOrderIds = await _context.Orders
                .Where(o => o.status == "Completed")
                .Select(o => o.order_id)
                .ToListAsync();

            var soldByInventory = await _context.OrderItems
                .Where(oi => completedOrderIds.Contains(oi.order_id))
                .GroupBy(oi => oi.inventory_id)
                .Select(g => new { InventoryId = g.Key, Qty = g.Sum(x => x.quantity) })
                .ToListAsync();

            var inventoryShoeMap = await _context.ShoeInventories
                .Select(i => new { i.inventory_id, i.shoe_id })
                .ToListAsync();

            var soldByShoe = soldByInventory
                .Join(inventoryShoeMap, s => s.InventoryId, i => i.inventory_id, (s, i) => new { i.shoe_id, s.Qty })
                .GroupBy(x => x.shoe_id)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));

            var productEntities = await _context.ShoeCatalogs
                .Where(s => s.status == "Active")
                .Include(s => s.Promotions)
                .Take(4)
                .ToListAsync();

            var products = productEntities.Select(s =>
            {
                var promotion = PromotionHelper.GetCurrentPromotion(s);
                return new ProductCardViewModel
                {
                    ShoeId = s.shoe_id,
                    ModelName = s.model_name,
                    Brand = s.brand,
                    Category = s.category,
                    OriginalPrice = s.default_price,
                    Price = PromotionHelper.GetEffectivePrice(s.default_price, promotion),
                    DiscountPercentage = promotion?.discount_percentage,
                    ImagePath = s.image_path,
                    TotalStock = _context.ShoeInventories.Where(i => i.shoe_id == s.shoe_id).Sum(i => i.quantity_in_stock)
                };
            }).ToList();

            // I-map yung rating at sold count (in-memory na, kasi galing sa dictionary)
            foreach (var p in products)
            {
                var stat = ratingStats.FirstOrDefault(r => r.ShoeId == p.ShoeId);
                p.AvgRating = stat?.AvgRating ?? 0;
                p.RatingCount = stat?.RatingCount ?? 0;
                p.SoldCount = soldByShoe.TryGetValue(p.ShoeId, out var qty) ? qty : 0;
            }

            return View(products);
        }

        // 2. Products Catalog Page
        public async Task<IActionResult> Products(string? category, string? searchString, string? sort, int page = 1)
        {
            const int pageSize = 8;

            var query = _context.ShoeCatalogs
            .Where(s => s.status == "Active")
            .AsQueryable();

            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(s => s.category == category);
            }

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(s => s.model_name.Contains(searchString) || s.brand.Contains(searchString));
            }

            var cutoffDate = DateTime.Now.AddDays(-14);

            query = sort switch
            {
                "newest" => query.Where(s => s.CreatedAt >= cutoffDate).OrderByDescending(s => s.CreatedAt),
                "price_asc" => query.OrderBy(s => s.default_price),
                "price_desc" => query.OrderByDescending(s => s.default_price),
                _ => query.OrderBy(s => s.model_name)
            };

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var productEntities = await query
                .Include(s => s.Promotions)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

                var products = productEntities.Select(s =>
                {
                    var promotion = PromotionHelper.GetCurrentPromotion(s);
                    return new ProductCardViewModel
                    {
                        ShoeId = s.shoe_id,
                        ModelName = s.model_name,
                        Brand = s.brand,
                        Category = s.category,
                        OriginalPrice = s.default_price,
                        Price = PromotionHelper.GetEffectivePrice(s.default_price, promotion),
                        DiscountPercentage = promotion?.discount_percentage,
                        ImagePath = s.image_path,
                        TotalStock = _context.ShoeInventories.Where(i => i.shoe_id == s.shoe_id).Sum(i => i.quantity_in_stock)
                    };
                }).ToList();

                var vm = new ProductsViewModel
                {
                    Products = products,
                    SelectedCategory = category,
                    SearchString = searchString,
                    Sort = sort,
                    CurrentPage = page,
                    TotalPages = totalPages,
                    TotalItems = totalItems,
                    PageSize = pageSize
                };

                return View(vm);
            }

        // 3. Product Details Page
        public async Task<IActionResult> Details(int id)
        {
    var shoe = await _context.ShoeCatalogs
                .Include(s => s.Images)
                .FirstOrDefaultAsync(s => s.shoe_id == id);

            if (shoe == null)
                return NotFound();

            var sizes = await _context.ShoeInventories
                .Where(i => i.shoe_id == id)
                .OrderBy(i => i.size)
                .Select(i => new SizeOptionViewModel
                {
                    InventoryId = i.inventory_id,
                    Size = i.size,
                    Stock = i.quantity_in_stock
                })
                .ToListAsync();

            // ⬇️ IDINAGDAG DITO
            foreach (var s in sizes)
            {
                s.Cm = ShoeSizeConverter.ToCm(s.Size);
            }

            var images = shoe.Images
                .OrderBy(img => img.DisplayOrder)
                .Select(img => img.FilePath)
                .ToList();

            if (!images.Any())
                images.Add(string.IsNullOrEmpty(shoe.image_path) ? "/images/no-image.png" : shoe.image_path);

            var ratingStats = await _context.ProductRatings
                .Where(r => r.shoe_id == id)
                .GroupBy(r => r.shoe_id)
                .Select(g => new { AvgRating = g.Average(x => x.rating), Count = g.Count() })
                .FirstOrDefaultAsync();

            var vm = new ProductDetailsViewModel
            {
                ShoeId = shoe.shoe_id,
                ModelName = shoe.model_name,
                Brand = shoe.brand,
                Category = shoe.category,
                Color = shoe.color,
                OriginalPrice = shoe.default_price,
                Price = PromotionHelper.GetEffectivePrice(shoe.default_price, PromotionHelper.GetCurrentPromotion(shoe)),
                DiscountPercentage = PromotionHelper.GetCurrentPromotion(shoe)?.discount_percentage,
                ImagePaths = images,
                Sizes = sizes,
                AvgRating = ratingStats?.AvgRating ?? 0,
                RatingCount = ratingStats?.Count ?? 0
            };

            return View(vm);
}

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rate(int shoeId, int rating)
        {
            var email = HttpContext.Session.GetString("CustomerUser");
            if (string.IsNullOrWhiteSpace(email))
                return Json(new { success = false, redirectToLogin = true });

            if (rating < 1 || rating > 5 || !await _context.ShoeCatalogs.AnyAsync(s => s.shoe_id == shoeId))
                return Json(new { success = false, message = "Please select a rating from 1 to 5." });

            var customerId = await _context.Customers
                .Where(c => c.email == email)
                .Select(c => (int?)c.customer_id)
                .FirstOrDefaultAsync();
            if (customerId == null)
                return Json(new { success = false, redirectToLogin = true });

            var hasDeliveredPurchase = await _context.Orders
                .Where(o => o.customer_id == customerId.Value && o.status == "Delivered")
                .SelectMany(o => o.OrderItems)
                .AnyAsync(item => item.ShoeInventory != null && item.ShoeInventory.shoe_id == shoeId);
            if (!hasDeliveredPurchase)
                return Json(new { success = false, message = "You can rate this product after a delivered purchase." });

            var existing = await _context.ProductRatings
                .FirstOrDefaultAsync(r => r.shoe_id == shoeId && r.customer_id == customerId.Value);
            if (existing == null)
                _context.ProductRatings.Add(new ProductRating { shoe_id = shoeId, customer_id = customerId.Value, rating = rating });
            else
                existing.rating = rating;

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Rating saved." });
        }

        public IActionResult About() => View();

        public IActionResult Contact() => View();
    }
}