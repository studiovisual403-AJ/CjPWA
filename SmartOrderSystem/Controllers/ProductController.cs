using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOrderSystem.Data;
using SmartOrderSystem.Models;
using SmartOrderSystem.Services;
using SmartOrderSystem.ViewModels;

namespace SmartOrderSystem.Controllers
{
    public class ProductController : AdminBaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly INotificationService _notificationService;

        public ProductController(ApplicationDbContext context, IWebHostEnvironment environment, INotificationService notificationService) : base(context)
        {
            _context = context;
            _environment = environment;
            _notificationService = notificationService;
        }

        public const long MaxUploadFileSizeInBytes = 5 * 1024 * 1024;
        public static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".jfif", ".png", ".webp" };
        public static readonly string[] AllowedContentTypes = { "image/jpeg", "image/jfif", "image/png", "image/webp" };

        private static readonly string[] Categories = { "Running", "Basketball", "Casual", "Lifestyle" };
        private static readonly string[] ProductStatuses = { "Active", "Inactive" };

        public async Task<IActionResult> Index(string search = "", int page = 1)
        {
            const int pageSize = 10;
            page = Math.Max(1, page);

            var query = _context.ShoeCatalogs.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.brand.Contains(search) || p.model_name.Contains(search));

            var totalCount = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Min(page, totalPages);

            var products = await query.Include(p => p.Images).OrderBy(p => p.shoe_id)
                .Include(p => p.Promotions)
                .OrderBy(p => p.shoe_id) 
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewData["Search"] = search;
            ViewData["CurrentPage"] = page;
            ViewData["TotalPages"] = totalPages;
            ViewData["TotalCount"] = totalCount;
            return View(products);
        }

        [HttpGet]
        public IActionResult Create()
        {
            ViewData["Categories"] = Categories;
            return View(new AddProductViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AddProductViewModel model)
        {
            ValidateProductFields(model.Brand, model.ModelName, model.Category, model.DefaultPrice, model.Status);
            ValidateInitialStock(model.SizesWithStocks);

            if (model.ProductImage != null)
            {
                var imageValidation = ValidateUploadFiles(new[] { model.ProductImage });
                if (!imageValidation.IsValid)
                    ModelState.AddModelError(nameof(model.ProductImage), imageValidation.ErrorMessage!);
            }

            if (!ModelState.IsValid)
            {
                ViewData["Categories"] = Categories;
                return View(model);
            }

            string? imagePath = null;
            try
            {
                if (model.ProductImage != null)
                    imagePath = await SaveUploadedFileAsync(model.ProductImage, Path.Combine(_environment.WebRootPath, "uploads", "products"), "/uploads/products/");

                await using var transaction = await _context.Database.BeginTransactionAsync();
                var product = new ShoeCatalog
                {
                    brand = model.Brand.Trim(),
                    model_name = model.ModelName.Trim(),
                    category = model.Category,
                    color = model.Color?.Trim(),
                    default_price = model.DefaultPrice,
                    status = model.Status,
                    image_path = imagePath
                };

                _context.ShoeCatalogs.Add(product);
                await _context.SaveChangesAsync();

                if (imagePath != null)
                {
                    _context.ProductImages.Add(new ProductImage
                    {
                        FileName = Path.GetFileName(imagePath),
                        FilePath = imagePath,
                        FileSize = model.ProductImage!.Length,
                        ShoeCatalogId = product.shoe_id,
                        IsCover = true,
                        DisplayOrder = 1
                    });
                }

                foreach (var stock in model.SizesWithStocks)
                {
                    _context.ShoeInventories.Add(new ShoeInventory
                    {
                        shoe_id = product.shoe_id,
                        size = stock.Key,
                        quantity_in_stock = stock.Value
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                if (product.status == "Active")
                {
                    await _notificationService.NotifyAllCustomersAsync(
                        "NewArrival",
                        "New Arrival",
                        $"{product.brand} {product.model_name} is now available. Check it out!",
                        shoeId: product.shoe_id);
                }

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                DeletePhysicalFile(imagePath);
                throw;
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.ShoeCatalogs.FindAsync(id);
            if (product == null) return NotFound();
            ViewData["Categories"] = Categories;
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ShoeCatalog model)
        {
            if (id != model.shoe_id) return NotFound();

            ValidateProductFields(model.brand, model.model_name, model.category, model.default_price, model.status);
            var product = await _context.ShoeCatalogs.FindAsync(id);
            if (product == null) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewData["Categories"] = Categories;
                return View(model);
            }

            product.brand = model.brand.Trim();
            product.model_name = model.model_name.Trim();
            product.category = model.category;
            product.color = model.color?.Trim();
            product.default_price = model.default_price;
            product.status = model.status;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> ManageImages(int? id)
        {
            if (id == null) return NotFound();
            var product = await _context.ShoeCatalogs
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.shoe_id == id);
            if (product == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(product.image_path) && !product.Images.Any())
            {
                product.Images.Add(new ProductImage
                {
                    FileName = Path.GetFileName(product.image_path),
                    FilePath = product.image_path,
                    FileSize = 0,
                    IsCover = true,
                    DisplayOrder = 1,
                    ShoeCatalogId = product.shoe_id
                });
                await _context.SaveChangesAsync();
            }

            var cover = product.Images.OrderBy(i => i.DisplayOrder).FirstOrDefault(i => i.IsCover)
                        ?? product.Images.OrderBy(i => i.DisplayOrder).FirstOrDefault();
            product.image_path = cover?.FilePath;
            await _context.SaveChangesAsync();
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadImages(int shoeId, List<IFormFile> imageFiles)
        {
            if (!await _context.ShoeCatalogs.AnyAsync(p => p.shoe_id == shoeId)) return NotFound();

            var validation = ValidateUploadFiles(imageFiles);
            if (!validation.IsValid)
            {
                TempData["Error"] = validation.ErrorMessage;
                return RedirectToAction(nameof(ManageImages), new { id = shoeId });
            }

            var savedPaths = new List<string>();
            try
            {
                var folder = Path.Combine(_environment.WebRootPath, "images");
                var displayOrder = await _context.ProductImages
                    .Where(i => i.ShoeCatalogId == shoeId)
                    .Select(i => (int?)i.DisplayOrder)
                    .MaxAsync() ?? 0;
                var hasCover = await _context.ProductImages.AnyAsync(i => i.ShoeCatalogId == shoeId && i.IsCover);

                foreach (var file in imageFiles)
                {
                    var path = await SaveUploadedFileAsync(file, folder, "/images/");
                    savedPaths.Add(path);
                    _context.ProductImages.Add(new ProductImage
                    {
                        FileName = Path.GetFileName(file.FileName),
                        FilePath = path,
                        FileSize = file.Length,
                        ShoeCatalogId = shoeId,
                        IsCover = !hasCover,
                        DisplayOrder = ++displayOrder
                    });
                    hasCover = true;
                }

                var product = await _context.ShoeCatalogs.FindAsync(shoeId);
                if (product != null)
                    product.image_path = await _context.ProductImages
                        .Where(i => i.ShoeCatalogId == shoeId && i.IsCover)
                        .Select(i => i.FilePath)
                        .FirstOrDefaultAsync() ?? savedPaths[0];

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(ManageImages), new { id = shoeId });
            }
            catch
            {
                foreach (var path in savedPaths) DeletePhysicalFile(path);
                throw;
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReplaceImage(int imageId, IFormFile imageFile)
        {
            var validation = ValidateUploadFiles(new[] { imageFile });
            if (!validation.IsValid) return BadRequest(validation.ErrorMessage);

            var image = await _context.ProductImages.FindAsync(imageId);
            if (image == null) return NotFound();

            var newPath = await SaveUploadedFileAsync(imageFile, Path.Combine(_environment.WebRootPath, "images"), "/images/");
            var oldPath = image.FilePath;
            image.FileName = Path.GetFileName(imageFile.FileName);
            image.FilePath = newPath;
            image.FileSize = imageFile.Length;

            if (image.IsCover)
            {
                var product = await _context.ShoeCatalogs.FindAsync(image.ShoeCatalogId);
                if (product != null) product.image_path = newPath;
            }

            try
            {
                await _context.SaveChangesAsync();
                DeletePhysicalFile(oldPath);
                return Ok();
            }
            catch
            {
                DeletePhysicalFile(newPath);
                throw;
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetAsCover(int imageId, int shoeId)
        {
            var images = await _context.ProductImages.Where(i => i.ShoeCatalogId == shoeId).ToListAsync();
            var selected = images.FirstOrDefault(i => i.Id == imageId);
            if (selected == null) return BadRequest("The image does not belong to this product.");

            foreach (var image in images) image.IsCover = image.Id == selected.Id;
            var product = await _context.ShoeCatalogs.FindAsync(shoeId);
            if (product != null) product.image_path = selected.FilePath;
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteImage(int imageId)
        {
            var image = await _context.ProductImages.FindAsync(imageId);
            if (image == null) return NotFound();

            var wasCover = image.IsCover;
            var productId = image.ShoeCatalogId;
            _context.ProductImages.Remove(image);
            await _context.SaveChangesAsync();
            DeletePhysicalFile(image.FilePath);

            if (wasCover)
            {
                var replacement = await _context.ProductImages
                    .Where(i => i.ShoeCatalogId == productId)
                    .OrderBy(i => i.DisplayOrder)
                    .FirstOrDefaultAsync();
                if (replacement != null)
                {
                    replacement.IsCover = true;
                    await _context.SaveChangesAsync();
                }

                var product = await _context.ShoeCatalogs.FindAsync(productId);
                if (product != null)
                {
                    product.image_path = replacement?.FilePath;
                    await _context.SaveChangesAsync();
                }
            }

            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateImageName(int imageId, string newFileName)
        {
            var image = await _context.ProductImages.FindAsync(imageId);
            if (image == null) return NotFound();
            if (string.IsNullOrWhiteSpace(newFileName) || newFileName.Length > 255)
                return BadRequest("A valid filename is required.");

            var safeFileName = Path.GetFileName(newFileName.Trim());
            if (string.IsNullOrWhiteSpace(safeFileName))
                return BadRequest("A valid filename is required.");

            image.FileName = safeFileName;
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReorderImages([FromBody] ReorderImagesModel? model)
        {
            if (model == null || model.ShoeId <= 0 || model.OrderedImageIds == null || model.OrderedImageIds.Count == 0)
                return BadRequest("A valid image order is required.");

            var requestedIds = model.OrderedImageIds.ToHashSet();
            if (requestedIds.Count != model.OrderedImageIds.Count)
                return BadRequest("Duplicate image IDs are not allowed.");

            var images = await _context.ProductImages.Where(i => i.ShoeCatalogId == model.ShoeId).ToListAsync();
            if (images.Count != requestedIds.Count || images.Any(i => !requestedIds.Contains(i.Id)))
                return BadRequest("All images must belong to the selected product.");

            var imageById = images.ToDictionary(i => i.Id);
            for (var index = 0; index < model.OrderedImageIds.Count; index++)
                imageById[model.OrderedImageIds[index]].DisplayOrder = index + 1;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> Promotion(int id)
        {
            var product = await _context.ShoeCatalogs.FindAsync(id);
            if (product == null) return NotFound();

            var promotion = await _context.ProductPromotions
                .Where(p => p.shoe_id == id)
                .OrderByDescending(p => p.promotion_id)
                .FirstOrDefaultAsync();
            ViewData["Product"] = product;

            return View(promotion ?? new ProductPromotion
            {
                shoe_id = id,
                start_date = DateTime.Today,
                end_date = DateTime.Today.AddDays(14),
                status = "Active"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Promotion(ProductPromotion model)
        {
            var product = await _context.ShoeCatalogs.FindAsync(model.shoe_id);
            if (product == null) return NotFound();

            ModelState.Remove(nameof(model.Shoe));
            if (model.end_date < model.start_date)
                ModelState.AddModelError(nameof(model.end_date), "End date must be after the start date.");
            if (model.discount_percentage <= 0 || model.discount_percentage > 100)
                ModelState.AddModelError(nameof(model.discount_percentage), "Discount must be between 1 and 100.");

            if (!ModelState.IsValid)
            {
                ViewData["Product"] = product;
                return View(model);
            }

            var isNewPromotion = model.promotion_id == 0;

            if (isNewPromotion)
            {
                model.Shoe = null;
                _context.ProductPromotions.Add(model);
            }
            else
            {
                var existing = await _context.ProductPromotions
                    .FirstOrDefaultAsync(p => p.promotion_id == model.promotion_id && p.shoe_id == model.shoe_id);
                if (existing == null) return NotFound();

                existing.campaign_name = model.campaign_name;
                existing.discount_percentage = model.discount_percentage;
                existing.start_date = model.start_date;
                existing.end_date = model.end_date;
                existing.status = model.status;
            }

            await _context.SaveChangesAsync();

            if (isNewPromotion && model.status == "Active")
            {
                await _notificationService.NotifyAllCustomersAsync(
                    "Promotion",
                    string.IsNullOrWhiteSpace(model.campaign_name) ? "New Promotion" : model.campaign_name,
                    $"Get {model.discount_percentage}% OFF on {product.brand} {product.model_name}! Valid until {model.end_date:MMMM dd, yyyy}.",
                    promotionId: model.promotion_id,
                    shoeId: model.shoe_id);
            }

            TempData["Success"] = "Promotion saved successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteAllPhysicalFiles() => Forbid();

        public static UploadValidationResult ValidateUploadFiles(IEnumerable<IFormFile>? files)
        {
            if (files == null || !files.Any())
                return new UploadValidationResult(false, "Please select at least one image.");

            foreach (var file in files)
            {
                if (file == null || file.Length <= 0)
                    return new UploadValidationResult(false, "Please select at least one image.");
                if (file.Length > MaxUploadFileSizeInBytes)
                    return new UploadValidationResult(false, "The file exceeds the 5MB size limit.");

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedImageExtensions.Contains(extension))
                    return new UploadValidationResult(false, "The file has an invalid format.");

                var contentType = file.ContentType?.ToLowerInvariant();
                if (!AllowedContentTypes.Contains(contentType))
                    return new UploadValidationResult(false, "Invalid image file.");

                if (!HasValidImageSignature(file, extension))
                    return new UploadValidationResult(false, "The uploaded file is not a valid image.");
            }

            return new UploadValidationResult(true, null);
        }

        private static bool HasValidImageSignature(IFormFile file, string extension)
        {
            using var stream = file.OpenReadStream();
            Span<byte> header = stackalloc byte[12];
            var bytesRead = stream.Read(header);
            if (extension is ".jpg" or ".jpeg" or ".jfif")
                return bytesRead >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
            if (extension == ".png")
                return bytesRead >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
            return extension == ".webp"
                && bytesRead >= 12
                && header[..4].SequenceEqual("RIFF"u8)
                && header[8..12].SequenceEqual("WEBP"u8);
        }

        private void ValidateProductFields(string? brand, string? modelName, string? category, decimal price, string? status)
        {
            if (string.IsNullOrWhiteSpace(brand)) ModelState.AddModelError("brand", "Brand is required.");
            if (string.IsNullOrWhiteSpace(modelName)) ModelState.AddModelError("model_name", "Model name is required.");
            if (string.IsNullOrWhiteSpace(category) || !Categories.Contains(category)) ModelState.AddModelError("category", "Select a valid category.");
            if (price <= 0) ModelState.AddModelError("default_price", "Price must be greater than 0.");
            if (string.IsNullOrWhiteSpace(status) || !ProductStatuses.Contains(status)) ModelState.AddModelError("status", "Select a valid status.");
        }

        private void ValidateInitialStock(Dictionary<int, int>? stocks)
        {
            if (stocks != null && stocks.Any(s => s.Key < 36 || s.Key > 45 || s.Value < 0))
                ModelState.AddModelError(nameof(AddProductViewModel.SizesWithStocks), "Sizes must be 36 to 45 and stock cannot be negative.");
        }

        private async Task<string> SaveUploadedFileAsync(IFormFile file, string folder, string publicPrefix)
        {
            Directory.CreateDirectory(folder);
            var fileName = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName).ToLowerInvariant();
            var filePath = Path.Combine(folder, fileName);
            await using var stream = new FileStream(filePath, FileMode.CreateNew);
            await file.CopyToAsync(stream);
            return publicPrefix + fileName;
        }

        private void DeletePhysicalFile(string? publicPath)
        {
            if (string.IsNullOrWhiteSpace(publicPath)) return;
            var relativePath = publicPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var root = Path.GetFullPath(_environment.WebRootPath + Path.DirectorySeparatorChar);
            var physicalPath = Path.GetFullPath(Path.Combine(_environment.WebRootPath, relativePath));
            if (physicalPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(physicalPath))
                System.IO.File.Delete(physicalPath);
        }
    }

    public sealed record UploadValidationResult(bool IsValid, string? ErrorMessage);

    public sealed class ReorderImagesModel
    {
        public int ShoeId { get; set; }
        public List<int> OrderedImageIds { get; set; } = new();
    }

    public sealed class ReorderItem
    {
        public int imageId { get; set; }
        public int displayOrder { get; set; }
    }
}