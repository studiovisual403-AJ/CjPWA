using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOrderSystem.Data;
using SmartOrderSystem.Helpers;
using SmartOrderSystem.Models;
using SmartOrderSystem.ViewModels;

namespace SmartOrderSystem.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const string SessionKey = "CheckoutState";

        public CheckoutController(ApplicationDbContext context)
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

        private CheckoutSessionState? GetState()
        {
            var json = HttpContext.Session.GetString(SessionKey);
            return json == null ? null : JsonSerializer.Deserialize<CheckoutSessionState>(json);
        }

        private void SaveState(CheckoutSessionState state)
        {
            HttpContext.Session.SetString(SessionKey, JsonSerializer.Serialize(state));
        }

        public async Task<IActionResult> Index()
        {
            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId == null)
                return RedirectToAction("Login", "Account");

            var cartItems = await _context.CartItems
                .Include(c => c.Shoe)
                    .ThenInclude(s => s.Promotions)
                .Where(c => c.CustomerId == customerId)
                .ToListAsync();

            if (!cartItems.Any())
            {
                TempData["Error"] = "Your cart is empty.";
                return RedirectToAction("Index", "Cart");
            }

            var state = new CheckoutSessionState { CartItemIds = cartItems.Select(c => c.CartItemId).ToList() };
            SaveState(state);

            var summary = await BuildOrderSummaryAsync(state, customerId.Value);
            return summary == null ? NotFound() : View(summary);
        }

        // ---- Order summary builders ----

        private async Task<CheckoutOrderSummaryViewModel?> BuildOrderSummaryFromReservationAsync(int reservationId)
        {
            var reservation = await _context.Reservations
                .Include(r => r.ShoeInventory)
                    .ThenInclude(si => si!.ShoeCatalog)
                .FirstOrDefaultAsync(r => r.reservation_id == reservationId);

            var catalog = reservation?.ShoeInventory?.ShoeCatalog;
            if (reservation == null || catalog == null)
                return null;

            var subtotal = catalog.default_price * reservation.quantity;
            var shippingFee = ShippingHelper.CalculateShipping(subtotal);

            return new CheckoutOrderSummaryViewModel
            {
                ShoeName = catalog.model_name,
                Brand = catalog.brand,
                ImagePath = catalog.image_path,
                Size = reservation.ShoeInventory!.size,
                Quantity = reservation.quantity,
                UnitPrice = catalog.default_price,
                Subtotal = subtotal,
                ShippingFee = shippingFee,
                Total = subtotal + shippingFee
            };
        }

        private async Task<CheckoutOrderSummaryViewModel?> BuildOrderSummaryFromInventoryAsync(int inventoryId, int quantity)
        {
            var inventory = await _context.ShoeInventories
                .Include(i => i.ShoeCatalog)
                .FirstOrDefaultAsync(i => i.inventory_id == inventoryId);

            var catalog = inventory?.ShoeCatalog;
            if (inventory == null || catalog == null || quantity <= 0)
                return null;

            var subtotal = catalog.default_price * quantity;
            var shippingFee = ShippingHelper.CalculateShipping(subtotal);

            return new CheckoutOrderSummaryViewModel
            {
                ShoeName = catalog.model_name,
                Brand = catalog.brand,
                ImagePath = catalog.image_path,
                Size = inventory.size,
                Quantity = quantity,
                UnitPrice = catalog.default_price,
                Subtotal = subtotal,
                ShippingFee = shippingFee,
                Total = subtotal + shippingFee
            };
        }

        private async Task<CheckoutOrderSummaryViewModel?> BuildOrderSummaryFromCartAsync(List<int> cartItemIds, int customerId)
        {
            var cartItems = await _context.CartItems
                .Include(c => c.Shoe)
                .Where(c => c.CustomerId == customerId && cartItemIds.Contains(c.CartItemId))
                .ToListAsync();

            if (!cartItems.Any())
                return null;

            var summary = new CheckoutOrderSummaryViewModel();
            foreach (var item in cartItems)
            {
                summary.Items.Add(new CheckoutCartItemViewModel
                {
                    ShoeName = item.Shoe.model_name,
                    Brand = item.Shoe.brand,
                    ImagePath = item.Shoe.image_path,
                    Size = item.Size,
                    Color = item.Color,
                    Quantity = item.Quantity,
                    UnitPrice = PromotionHelper.GetEffectivePrice(item.Shoe.default_price, PromotionHelper.GetCurrentPromotion(item.Shoe))
                });
            }

            summary.Subtotal = summary.Items.Sum(i => i.LineTotal);
            summary.ShippingFee = ShippingHelper.CalculateShipping(summary.Subtotal);
            summary.Total = summary.Subtotal + summary.ShippingFee;
            return summary;
        }

        private async Task<CheckoutOrderSummaryViewModel?> BuildOrderSummaryAsync(CheckoutSessionState state, int customerId)
        {
            if (state.CartItemIds.Any())
                return await BuildOrderSummaryFromCartAsync(state.CartItemIds, customerId);

            if (state.ReservationId.HasValue)
                return await BuildOrderSummaryFromReservationAsync(state.ReservationId.Value);

            if (state.InventoryId.HasValue && state.Quantity.HasValue)
                return await BuildOrderSummaryFromInventoryAsync(state.InventoryId.Value, state.Quantity.Value);

            return null;
        }

        // GET: /Checkout/Delivery?reservationId=5              (reservation-based entry)
        // GET: /Checkout/Delivery?inventoryId=9&quantity=1      (direct buy-now entry)
        // GET: /Checkout/Delivery                                (balik mula Payment step)
        public async Task<IActionResult> Delivery(int? reservationId, int? inventoryId, int? quantity)
        {
            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId == null)
                return RedirectToAction("Login", "Account");

            CheckoutSessionState? state;

            if (reservationId.HasValue)
            {
                var reservation = await _context.Reservations
                    .FirstOrDefaultAsync(r => r.reservation_id == reservationId.Value && r.customer_id == customerId);

                if (reservation == null)
                    return NotFound();

                if (reservation.status != "Approved")
                {
                    TempData["Error"] = "Only approved reservations can proceed to checkout.";
                    return RedirectToAction("Details", "Reservation", new { id = reservationId.Value });
                }

                if (reservation.converted_order_id.HasValue)
                {
                    TempData["Error"] = "This reservation has already been converted to an order.";
                    return RedirectToAction("Details", "Reservation", new { id = reservationId.Value });
                }

                state = new CheckoutSessionState { ReservationId = reservationId.Value };
                await PrefillAddressAsync(state, customerId.Value);
                SaveState(state);
            }
            else if (inventoryId.HasValue)
            {
                var qty = quantity.GetValueOrDefault(1);
                if (qty <= 0) qty = 1;

                var inventory = await _context.ShoeInventories
                    .Include(i => i.ShoeCatalog)
                    .FirstOrDefaultAsync(i => i.inventory_id == inventoryId.Value);

                if (inventory == null || inventory.ShoeCatalog == null)
                    return NotFound();

                if (qty > inventory.quantity_in_stock)
                {
                    TempData["Error"] = "Selected quantity exceeds available stock.";
                    return RedirectToAction("Details", "Shop", new { id = inventory.shoe_id });
                }

                state = new CheckoutSessionState { InventoryId = inventoryId.Value, Quantity = qty };
                await PrefillAddressAsync(state, customerId.Value);
                SaveState(state);
            }
            else
            {
                state = GetState();
                if (state == null)
                {
                    TempData["Error"] = "Your checkout session has expired. Please start again.";
                    return RedirectToAction("Index", "Shop");
                }
            }

            var summary = await BuildOrderSummaryAsync(state, customerId.Value);
            if (summary == null)
                return NotFound();

            var savedAddresses = await _context.CustomerAddresses
                .Where(a => a.customer_id == customerId.Value)
                .OrderByDescending(a => a.is_default)
                .ThenByDescending(a => a.updated_at)
                .ToListAsync();

            // Hanapin kung alin sa saved addresses ang tumutugma sa laman ng state (yung napili na dati)
            var selected = savedAddresses.FirstOrDefault(a =>
                a.recipient_name == state.FullName &&
                a.contact_number == state.PhoneNumber &&
                a.province == state.Province &&
                a.city == state.City &&
                a.barangay == state.Barangay &&
                a.street_address == state.Street &&
                a.landmark == state.Landmark) ?? savedAddresses.FirstOrDefault();

            var vm = new CheckoutDeliveryViewModel
            {
                FullName = state.FullName ?? string.Empty,
                PhoneNumber = state.PhoneNumber ?? string.Empty,
                Province = state.Province ?? string.Empty,
                City = state.City ?? string.Empty,
                Barangay = state.Barangay ?? string.Empty,
                Street = state.Street ?? string.Empty,
                Landmark = state.Landmark,
                OrderSummary = summary,
                SavedAddresses = savedAddresses,
                SelectedAddressId = selected?.address_id
            };

            return View(vm);
        }

        private async Task PrefillAddressAsync(CheckoutSessionState state, int customerId)
        {
            var defaultAddress = await _context.CustomerAddresses
                .Where(a => a.customer_id == customerId)
                .OrderByDescending(a => a.is_default)
                .ThenByDescending(a => a.updated_at)
                .FirstOrDefaultAsync();

            if (defaultAddress != null)
            {
                state.FullName = defaultAddress.recipient_name;
                state.PhoneNumber = defaultAddress.contact_number;
                state.Province = defaultAddress.province;
                state.City = defaultAddress.city;
                state.Barangay = defaultAddress.barangay;
                state.Street = defaultAddress.street_address;
                state.Landmark = defaultAddress.landmark;
            }
        }

        // POST: /Checkout/Delivery - kumukuha na ng piniling saved address mula sa
        // Change Address modal sa halip na manual na text fields
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delivery(int SelectedAddressId)
        {
            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId == null)
                return RedirectToAction("Login", "Account");

            var state = GetState();
            if (state == null)
            {
                TempData["Error"] = "Your checkout session has expired. Please start again.";
                return RedirectToAction("Index", "Shop");
            }

            var address = await _context.CustomerAddresses
                .FirstOrDefaultAsync(a => a.address_id == SelectedAddressId && a.customer_id == customerId.Value);

            if (address == null)
            {
                TempData["Error"] = "Please select a valid delivery address.";
                return RedirectToAction(nameof(Delivery));
            }

            state.FullName = address.recipient_name;
            state.PhoneNumber = address.contact_number;
            state.Province = address.province;
            state.City = address.city;
            state.Barangay = address.barangay;
            state.Street = address.street_address;
            state.Landmark = address.landmark;
            state.DeliveryCompleted = true;
            SaveState(state);

            return RedirectToAction(nameof(Payment));
        }

        public async Task<IActionResult> Payment()
        {
            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId == null)
                return RedirectToAction("Login", "Account");

            var state = GetState();
            if (state == null || !state.DeliveryCompleted)
            {
                TempData["Error"] = "Please complete delivery details first.";
                return RedirectToAction(nameof(Delivery));
            }

            var summary = await BuildOrderSummaryAsync(state, customerId.Value);
            if (summary == null)
                return NotFound();

            var vm = new CheckoutPaymentViewModel
            {
                PaymentMethod = state.PaymentMethod ?? "Cash on Delivery",
                OrderSummary = summary
            };

            return View(vm);
        }

        // POST: /Checkout/Confirmation - creates the order. POST-only: a state-changing
        // operation must never run from a safe GET request (prefetch/navigation/link).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirmation(string PaymentMethod)
        {
            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId == null)
                return RedirectToAction("Login", "Account");

            var state = GetState();
            if (state == null || !state.DeliveryCompleted)
            {
                TempData["Error"] = "Please complete the checkout steps first.";
                return RedirectToAction(nameof(Delivery));
            }

            state.PaymentMethod = string.IsNullOrWhiteSpace(PaymentMethod) ? "Cash on Delivery" : PaymentMethod;
            state.PaymentCompleted = true;
            SaveState(state);

            // Idempotency guard - iwas duplicate order kapag nag-refresh
            if (state.CreatedOrderId.HasValue)
                return RedirectToAction(nameof(Confirmation), new { orderId = state.CreatedOrderId.Value });

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            Order order;

            if (state.CartItemIds.Any())
            {
                var cartItems = await _context.CartItems
                    .Include(c => c.Shoe)
                    .Where(c => c.CustomerId == customerId && state.CartItemIds.Contains(c.CartItemId))
                    .ToListAsync();

                if (!cartItems.Any())
                    return NotFound();

                var cartInventory = new List<(CartItem Item, ShoeInventory Inventory)>();
                foreach (var item in cartItems)
                {
                    if (item.Quantity <= 0)
                    {
                        TempData["Error"] = $"Invalid quantity for {item.Shoe.model_name}.";
                        HttpContext.Session.Remove(SessionKey);
                        return RedirectToAction("Index", "Cart");
                    }

                    if (!decimal.TryParse(item.Size, out var parsedSize))
                    {
                        TempData["Error"] = $"Selected size for {item.Shoe.model_name} is invalid.";
                        HttpContext.Session.Remove(SessionKey);
                        return RedirectToAction("Index", "Cart");
                    }

                    var inventory = await _context.ShoeInventories
                    .Include(i => i.ShoeCatalog)
                    .FirstOrDefaultAsync(i => i.shoe_id == item.ShoeId && i.size == parsedSize);

                    if (inventory == null || inventory.quantity_in_stock < item.Quantity)
                    {
                        TempData["Error"] = $"Selected quantity for {item.Shoe.model_name} is no longer available.";
                        HttpContext.Session.Remove(SessionKey);
                        return RedirectToAction("Index", "Cart");
                    }

                    cartInventory.Add((item, inventory));
                }

                var address = await CreateAddressAsync(state, customerId.Value);
                var subtotal = cartItems.Sum(i => PromotionHelper.GetEffectivePrice(i.Shoe.default_price, PromotionHelper.GetCurrentPromotion(i.Shoe)) * i.Quantity);
                var shippingFee = ShippingHelper.CalculateShipping(subtotal);

                order = new Order
                {
                    customer_id = customerId.Value,
                    address_id = address.address_id,
                    total_amount = subtotal + shippingFee,
                    status = "Confirmed",
                    payment_method = state.PaymentMethod ?? "Cash on Delivery",
                    payment_status = "Unpaid",
                    order_date = DateTime.Now,
                    shipping_status = "Confirmed"
                };
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                foreach (var entry in cartInventory)
                {
                    _context.OrderItems.Add(new OrderItem
                    {
                        order_id = order.order_id,
                        inventory_id = entry.Inventory.inventory_id,
                        quantity = entry.Item.Quantity,
                        item_price = PromotionHelper.GetEffectivePrice(entry.Item.Shoe.default_price, PromotionHelper.GetCurrentPromotion(entry.Item.Shoe))
                    });
                    entry.Inventory.quantity_in_stock -= entry.Item.Quantity;
                    _context.CartItems.Remove(entry.Item);
                }
                await _context.SaveChangesAsync();
            }
            else if (state.ReservationId.HasValue)
            {
                var reservation = await _context.Reservations
                    .Include(r => r.ShoeInventory)
                        .ThenInclude(si => si!.ShoeCatalog)
                    .FirstOrDefaultAsync(r => r.reservation_id == state.ReservationId.Value && r.customer_id == customerId);

                if (reservation == null)
                    return NotFound();

                if (reservation.status != "Approved" || reservation.converted_order_id.HasValue)
                {
                    TempData["Error"] = "This reservation is no longer eligible for checkout.";
                    HttpContext.Session.Remove(SessionKey);
                    return RedirectToAction("Index", "Reservation");
                }

                var inventory = reservation.ShoeInventory;
                var catalog = inventory?.ShoeCatalog;
                if (inventory == null || catalog == null || reservation.quantity <= 0)
                {
                    TempData["Error"] = "This reservation has invalid product information.";
                    return RedirectToAction("Details", "Reservation", new { id = state.ReservationId.Value });
                }

                var address = await CreateAddressAsync(state, customerId.Value);

                var effectivePrice = PromotionHelper.GetEffectivePrice(catalog.default_price, PromotionHelper.GetCurrentPromotion(catalog));
                var subtotal = effectivePrice * reservation.quantity;
                var shippingFee = ShippingHelper.CalculateShipping(subtotal);

                order = new Order
                {
                    customer_id = customerId.Value,
                    address_id = address.address_id,
                    total_amount = subtotal + shippingFee,
                    status = "Confirmed",
                    payment_method = state.PaymentMethod ?? "Cash on Delivery",
                    payment_status = "Unpaid",
                    order_date = DateTime.Now,
                    shipping_status = "Confirmed"
                };
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                _context.OrderItems.Add(new OrderItem
                {
                    order_id = order.order_id,
                    inventory_id = inventory.inventory_id,
                    quantity = reservation.quantity,
                    item_price = effectivePrice
                });

                reservation.status = "Converted";
                reservation.converted_order_id = order.order_id;
                await _context.SaveChangesAsync();
            }
            else if (state.InventoryId.HasValue && state.Quantity.HasValue)
            {
                var inventory = await _context.ShoeInventories
                    .Include(i => i.ShoeCatalog)
                    .FirstOrDefaultAsync(i => i.inventory_id == state.InventoryId.Value);

                var catalog = inventory?.ShoeCatalog;
                if (inventory == null || catalog == null)
                    return NotFound();

                var qty = state.Quantity.Value;
                if (qty <= 0 || qty > inventory.quantity_in_stock)
                {
                    TempData["Error"] = "Selected quantity is no longer available.";
                    HttpContext.Session.Remove(SessionKey);
                    return RedirectToAction("Details", "Shop", new { id = inventory.shoe_id });
                }

                var address = await CreateAddressAsync(state, customerId.Value);

                var effectivePrice = PromotionHelper.GetEffectivePrice(catalog.default_price, PromotionHelper.GetCurrentPromotion(catalog));
                var subtotal = effectivePrice * qty;
                var shippingFee = ShippingHelper.CalculateShipping(subtotal);

                order = new Order
                {
                    customer_id = customerId.Value,
                    address_id = address.address_id,
                    total_amount = subtotal + shippingFee,
                    status = "Confirmed",
                    payment_method = state.PaymentMethod ?? "Cash on Delivery",
                    payment_status = "Unpaid",
                    order_date = DateTime.Now,
                    shipping_status = "Confirmed"
                };
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                _context.OrderItems.Add(new OrderItem
                {
                    order_id = order.order_id,
                    inventory_id = inventory.inventory_id,
                    quantity = qty,
                    item_price = effectivePrice
                });

                // Direct buy-now - walang reservation na naghold ng stock, kaya bawasan dito
                inventory.quantity_in_stock -= qty;
                await _context.SaveChangesAsync();
            }
            else
            {
                return NotFound();
            }

            await transaction.CommitAsync();

            state.CreatedOrderId = order.order_id;
            SaveState(state);

            return RedirectToAction(nameof(Confirmation), new { orderId = order.order_id });
        }

        [HttpGet]
        public async Task<IActionResult> Confirmation(int orderId)
        {
            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId == null)
                return RedirectToAction("Login", "Account");

            return await ShowConfirmation(orderId, customerId.Value);
        }

        // Iniiwasan na dito ang paggawa ng duplicate CustomerAddress rows: kung meron nang
        // eksaktong kaparehong address ang customer, gagamitin na lang uli iyon sa halip
        // na gumawa ng bagong row sa bawat checkout.
        private async Task<CustomerAddress> CreateAddressAsync(CheckoutSessionState state, int customerId)
        {
            var fullName = state.FullName ?? string.Empty;
            var phone = state.PhoneNumber ?? string.Empty;
            var province = state.Province ?? string.Empty;
            var city = state.City ?? string.Empty;
            var barangay = state.Barangay ?? string.Empty;
            var street = state.Street ?? string.Empty;
            var landmark = state.Landmark;

            var existing = await _context.CustomerAddresses.FirstOrDefaultAsync(a =>
                a.customer_id == customerId &&
                a.recipient_name == fullName &&
                a.contact_number == phone &&
                a.province == province &&
                a.city == city &&
                a.barangay == barangay &&
                a.street_address == street &&
                a.landmark == landmark);

            if (existing != null)
            {
                existing.updated_at = DateTime.Now;
                await _context.SaveChangesAsync();
                return existing;
            }

            var hasAnyAddress = await _context.CustomerAddresses.AnyAsync(a => a.customer_id == customerId);

            var address = new CustomerAddress
            {
                customer_id = customerId,
                recipient_name = fullName,
                contact_number = phone,
                province = province,
                city = city,
                barangay = barangay,
                street_address = street,
                landmark = landmark,
                address_type = "Shipping",
                is_default = !hasAnyAddress,
                created_at = DateTime.Now,
                updated_at = DateTime.Now
            };
            _context.CustomerAddresses.Add(address);
            await _context.SaveChangesAsync();
            return address;
        }

        private async Task<IActionResult> ShowConfirmation(int orderId, int customerId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.ShoeInventory)
                        .ThenInclude(si => si!.ShoeCatalog)
                .FirstOrDefaultAsync(o => o.order_id == orderId);

            if (order == null || order.customer_id != customerId)
                return NotFound();

            var firstItem = order.OrderItems.FirstOrDefault();
            var catalog = firstItem?.ShoeInventory?.ShoeCatalog;

            var vm = new CheckoutConfirmationViewModel
            {
                OrderId = order.order_id,
                OrderDate = order.order_date,
                PaymentMethod = order.payment_method ?? "Cash on Delivery",
                TotalAmount = order.total_amount,
                ShoeName = catalog?.model_name ?? "Unknown",
                Brand = catalog?.brand ?? "Unknown",
                ImagePath = catalog?.image_path,
                Size = firstItem?.ShoeInventory?.size ?? 0,
                Quantity = firstItem?.quantity ?? 0,
                UnitPrice = firstItem?.item_price ?? 0
            };

            return View("Confirmation", vm);
        }
    }
}