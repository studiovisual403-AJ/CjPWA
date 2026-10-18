# SmartOrderSystem Full Manual Test Plan

## Discovered controllers and views

| Controller | Views / implemented features |
|---|---|
| `AccountController` | Login/register, password recovery/security questions, account/profile/password, saved addresses |
| `AdminAccountController` | Admin login, profile, settings, notifications; no admin registration or user-management actions |
| `ShopController` | Home/catalog search and category/sort, product details, ratings, About, Contact |
| `CartController` | Cart view, add/update/remove/clear |
| `CheckoutController` | Cart checkout, buy-now, reservation checkout, delivery address, COD, confirmation |
| `OrderController` | Customer order list and details |
| `ReservationController` | Customer reservation list/create/cancel/details/checkout |
| `ProductController` | Admin product create/edit/list, image management, promotion create/edit/email |
| `InventoryController` | Admin inventory, restock, reorder-point report/export |
| `OrdersManagementController` | Admin order list/details, confirm/cancel/status pipeline, payment, customer notification |
| `ReservationManagementController` | Admin reservation list/details/approve/cancel |
| `DashboardController`, `AdminAlertsController` | Admin dashboard and alert summary |
| `SalesController` | Admin sales report, data endpoint, PDF export |
| `NotificationController` | Customer notification list and mark-read |
| `HomeController` | Home, Privacy, error page |

`AdminAccount/Notifications` is an admin page; customer notifications are served by `NotificationController`. There is no admin user-management controller/view in the project. The test plan below covers implemented modules only.

## Test setup

Use a non-production test deployment and test customer accounts A and B. Prepare one active product with stock, one zero-stock size, one saved address for each customer, and an admin account. Use test email recipients that have explicitly opted in. Do not use production customer email addresses for Brevo tests.

## 1. Authentication

| ID | Steps | Expected result | Priority (High/Med/Low) | Pass/Fail |
|---|---|---|---|---|
| AUTH-01 | Register with a valid name, unique email, phone, and password meeting the displayed rules. | Account is created and login succeeds with those credentials. | High | |
| AUTH-02 | Register again using the same email with different letter casing. | Duplicate is rejected; no second account is created. | High | |
| AUTH-03 | Submit registration with malformed email, short name, invalid phone, or weak password. | Validation message appears and no account is created. | Med | |
| AUTH-04 | Log in with a valid email and wrong password. | Login is rejected without opening customer pages. | High | |
| AUTH-05 | Log in successfully, then use Logout and press browser Back. | Session is cleared; protected customer pages require login. | High | |
| AUTH-06 | While logged out, open `/Account/MyAccount`, `/Account/MyAddresses`, `/Order`, `/Cart`, and `/Checkout/Payment` directly. | Each protected customer action redirects to login or otherwise denies access; no private data appears. | High | |
| AUTH-07 | Log in and leave the browser idle for over 30 minutes, then open a protected page or submit checkout. | Expired session is treated as logged out; no order is created from stale checkout state. | High | |
| AUTH-08 | Use Forgot Password with an unknown email, then with a known email and wrong security answers. | Unknown account is not advanced to reset; wrong answers are rejected and repeated failures trigger the account lockout message. | High | |
| AUTH-09 | Complete security verification, wait over 10 minutes, then submit password reset; repeat within 10 minutes with valid input. | Expired reset authorization is rejected; valid in-window reset succeeds and the new password works. | High | |
| AUTH-10 | Submit customer and admin login forms with blank fields and invalid credentials. | Validation or generic login error appears; neither role is granted. | High | |

## 2. Roles and authorization

| ID | Steps | Expected result | Priority (High/Med/Low) | Pass/Fail |
|---|---|---|---|---|
| ROLE-01 | While logged out, open `/Dashboard`, `/Product`, `/Inventory`, `/OrdersManagement`, `/ReservationManagement`, `/Sales`, and `/AdminAlerts/Summary`. | Admin pages redirect to admin login or deny access; no admin data is returned. | High | |
| ROLE-02 | Log in as customer A and open the same admin URLs directly. | Customer is denied and remains unable to view or change admin data. | High | |
| ROLE-03 | Log in as admin and open `/Dashboard`, `/Product`, `/Inventory`, and `/OrdersManagement`. | Admin pages load; logout then makes those URLs deny access. | High | |
| ROLE-04 | As customer A, open an order details URL for customer B's order by changing the order ID. | Response is not found/denied and reveals no order, address, or item data. | High | |
| ROLE-05 | As customer A, change an address ID in `/Account/GetAddress/{id}` to customer B's address. | Request returns unauthorized/not found and returns no address fields. | High | |
| ROLE-06 | As customer A, submit a delete/default-address form using customer B's address ID. | Request is denied/not found; B's address remains unchanged. | High | |
| ROLE-07 | As customer A, change the reservation ID in reservation details/checkout URL to B's reservation. | No reservation details or checkout is shown; no order is created. | High | |
| ROLE-08 | As customer A, edit cart item IDs or form fields to reference customer B's cart item, then update/remove. | B's cart remains unchanged; A cannot access or modify it. | High | |

## 3. Product catalog

| ID | Steps | Expected result | Priority (High/Med/Low) | Pass/Fail |
|---|---|---|---|---|
| CAT-01 | Open `/Shop/Products`; search by a known brand and by a known model name. | Matching active products appear; unrelated products are excluded. | Med | |
| CAT-02 | Filter by each available category and use name, price ascending/descending, and newest sorting. | Results match the selected category/sort and paging remains usable. | Med | |
| CAT-03 | Open an existing product detail and a nonexistent product ID. | Existing product shows correct images, sizes, and stock; nonexistent ID returns a not-found page. | Med | |
| CAT-04 | Open a size with zero stock and try add-to-cart and buy-now. | Unavailable size cannot be purchased; user gets a clear stock message and no order is created. | High | |
| CAT-05 | With an active promotion, compare the displayed price and discount on catalog and product detail pages. | Active discounted price and discount are displayed consistently; expired/inactive promotion is not applied. | High | |
| CAT-06 | Search with no matching text and visit a page number beyond the available range. | Empty results are handled without an error; paging does not expose invalid data or crash. | Low | |
| CAT-07 | As a logged-in customer, rate a shoe without a delivered order. | Rating is rejected with the delivered-purchase message and no rating is saved. | Med | |
| CAT-08 | After customer A has a Delivered order containing the shoe, submit rating 1, then rating 5. | Valid rating saves; changing it updates the existing rating rather than creating duplicates. | Med | |

## 4. Cart

| ID | Steps | Expected result | Priority (High/Med/Low) | Pass/Fail |
|---|---|---|---|---|
| CART-01 | As customer A, add an in-stock size and inspect cart. | Item, size, quantity, price, subtotal, and shipping display correctly. | High | |
| CART-02 | Add the same shoe/size again while stock permits. | Existing cart line quantity increases once and cart count/subtotal update. | High | |
| CART-03 | Try to add quantity 0, a negative quantity, and more than available stock (edit request if UI blocks it). | Invalid or over-stock quantity is rejected; stock and cart remain valid. | High | |
| CART-04 | Update a cart line to 0, negative, and above available stock. | Invalid update is rejected with a message; existing quantity is not corrupted. | High | |
| CART-05 | Update quantity to a valid value within stock. | Cart line and totals update to the new quantity. | High | |
| CART-06 | Remove one line, then clear the cart. | Removed line disappears; clear removes only the logged-in customer's cart. | Med | |
| CART-07 | Open cart with no items, then try to start checkout. | Empty state is shown and checkout does not create an order. | Med | |
| CART-08 | Add an item, log out, then log back in as A; also log in as B and inspect cart. | A's cart persists after re-login; B does not see A's cart. | High | |
| CART-09 | With an active promotion, compare cart price and line total against the catalog price. | Cart applies the active promotion consistently and computes the quantity total correctly. | High | |

## 5. Checkout and orders

| ID | Steps | Expected result | Priority (High/Med/Low) | Pass/Fail |
|---|---|---|---|---|
| ORD-01 | Add an item, choose a saved address, select COD, and place the order. | Exactly one order is saved with correct customer, address, items, total, COD method, Unpaid status, and reduced stock; cart is cleared. | High | |
| ORD-02 | On product details, start buy-now with a valid size/quantity and complete COD checkout. | One order is created for the selected inventory/quantity; total and stock are correct. | High | |
| ORD-03 | Create a reservation, have admin approve it, then use reservation checkout. | Approved reservation converts to one order, points to that order, and does not deduct the reserved quantity a second time. | High | |
| ORD-04 | Attempt checkout from a Pending, expired, cancelled, or already converted reservation. | Checkout is refused and no duplicate order is created. | High | |
| ORD-05 | Double-click Place Order or resend the same confirmation form with the same checkout key. | Only one order is created; repeat submission reaches the existing order confirmation without an error page. | High | |
| ORD-06 | In browser developer tools, change PaymentMethod to an unsupported value and submit. | Request is rejected or returned to payment with an error; no order is created. | High | |
| ORD-07 | Tamper with address ID to another customer's address, order ID, buy-now inventory ID, or quantity in the request. | Another customer's address/order cannot be used or viewed; unavailable/invalid quantity cannot create a bad order. | High | |
| ORD-08 | With an active promotion, compare catalog price, checkout summary, and saved `OrderItem` price/order total. | Same effective item price is used at all three points and total equals item totals plus applicable shipping. | High | |
| ORD-09 | Open `/Order` as customer A and inspect tabs, sort, then open own order details and alter the ID to B's. | List and details show only A's orders; sorting/status counts are consistent; B's order is denied. | High | |
| ORD-10 | As admin, advance an order Confirmed → Preparing → In Transit → Delivered; omit courier/tracking for In Transit, then provide them. | Invalid transition and missing courier/tracking are rejected; valid transitions persist and appear in customer order details. | High | |
| ORD-11 | Try to mark a non-Delivered order paid; then mark a Delivered COD order paid and try cancel after shipment/completion. | Early payment and late cancellation are refused; valid COD payment becomes Paid/Completed and is not paid twice. | High | |

## 6. Saved addresses

| ID | Steps | Expected result | Priority (High/Med/Low) | Pass/Fail |
|---|---|---|---|---|
| ADDR-01 | Add a complete address with recipient, phone, province, city, barangay, street, and optional postal/landmark. | Address appears in My Addresses; first address becomes default. | High | |
| ADDR-02 | Save an address with each required field blank in turn. | Save is rejected with validation feedback and no incomplete address is added. | Med | |
| ADDR-03 | Edit an existing address and reload the page. | Updated fields persist and remain associated with the same customer. | Med | |
| ADDR-04 | Add a second address and set it as default. | Exactly the selected address is shown as default. | Med | |
| ADDR-05 | Delete a non-default address. | Address is removed and other addresses remain intact. | Med | |
| ADDR-06 | Delete the default address when another address exists. | Address is removed and another remaining address becomes default. | Med | |
| ADDR-07 | Delete an address already referenced by an order. | Deletion is refused with a clear message; order address/history remains intact. | High | |
| ADDR-08 | In checkout, select a saved address, then tamper the POST with another customer's address ID. | Own address proceeds to payment; foreign address is rejected and no order is created. | High | |

## 7. Promotions

| ID | Steps | Expected result | Priority (High/Med/Low) | Pass/Fail |
|---|---|---|---|---|
| PROMO-01 | As admin, create a promotion with valid name, 1–100% discount, start/end dates, and Active status. | Promotion saves and active products display its discount. | High | |
| PROMO-02 | Submit discount 0, negative, above 100, or end date before start date. | Invalid promotion is rejected and no invalid pricing is applied. | High | |
| PROMO-03 | Edit an existing promotion and change discount, dates, and status. | Saved details update; inactive promotion stops affecting prices. | High | |
| PROMO-04 | Set promotion start date in future, end date in past, or status Inactive. | Promotion is not displayed/applied outside its active dates/status. | High | |
| PROMO-05 | With one active promotion, compare product page, cart, checkout summary, and saved order item/total. | Effective price agrees across every stage; saved order total reflects the same discount. | High | |
| PROMO-06 | Create two overlapping active promotions for one shoe, then refresh the product/cart/checkout. | Price is deterministic and matches the promotion selection rule used by the app; no inconsistent totals occur. | High | |
| PROMO-07 | Use an unusual campaign name containing ampersands, quotes, and non-English characters. | Name saves and displays without broken markup or script execution. | Med | |
| PROMO-08 | Create an active promotion and check opted-in customer notifications; then create an inactive promotion. | New active promotion notifies opted-in customers only; inactive promotion does not broadcast as active. | Med | |

## 8. Promotion email (Brevo)

| ID | Steps | Expected result | Priority (High/Med/Low) | Pass/Fail |
|---|---|---|---|---|
| MAIL-01 | Register or edit profile with promotion consent enabled; verify the customer is eligible for a send. | Consent is saved and the customer can receive a promotion email. | High | |
| MAIL-02 | Turn promotion consent off, then send a campaign to opted-in recipients. | Opted-out customer receives no campaign email. | High | |
| MAIL-03 | With valid Brevo settings and an active promotion, send to a small test group of opted-in addresses. | Send result reports sent/failed counts; email subject/body and account-settings link are usable. | High | |
| MAIL-04 | Send an inactive or out-of-date promotion. | Send is refused with the inactive/date message; no email is sent. | High | |
| MAIL-05 | Configure more than 300 opted-in recipients and attempt sending. | App refuses before sending and reports the 300-recipient limit. | High | |
| MAIL-06 | Send one promotion successfully, then immediately send that same promotion again. | Second attempt is refused with the already-sent-today message; no repeated campaign is sent. | High | |
| MAIL-07 | Temporarily use invalid Brevo credentials/host in a test environment and send. | Failure is reported clearly and the app remains usable; no success is falsely reported. | Med | |
| MAIL-08 | Send when no customers have opted in. | App reports no eligible recipients and sends nothing. | Med | |

## 9. Admin product, inventory, and order operations

| ID | Steps | Expected result | Priority (High/Med/Low) | Pass/Fail |
|---|---|---|---|---|
| ADM-01 | As admin, create a product with valid fields, one or more sizes/stocks, and a valid image. | Product and size inventory appear in admin and customer catalog. | High | |
| ADM-02 | Create/edit a product with missing/invalid price, status, category, or size/stock input. | Invalid values are rejected; valid existing product data remains unchanged. | High | |
| ADM-03 | Upload an unsupported image type, empty file, or image over 5 MB. | Upload is rejected and no invalid file becomes a product image. | Med | |
| ADM-04 | Edit product name, price, category, and Active/Inactive status. | Changes persist; inactive product is absent from customer catalog and cannot be ordered. | High | |
| ADM-05 | Upload multiple images, set another cover, replace/delete an image, and reorder. | Images and cover display as selected; product remains available after changes. | Med | |
| ADM-06 | Restock an existing SKU/size with valid positive quantity; then test zero, negative, fractional, out-of-range size, malformed SKU, and nonexistent SKU. | Valid restock increases exactly that size; every invalid request is rejected without changing stock. | High | |
| ADM-07 | Attempt to confirm an order with insufficient current stock, then restore stock and confirm a valid eligible Pending order. | Insufficient-stock confirmation is refused; valid confirmation changes status and deducts stock once. | High | |
| ADM-08 | Cancel Pending and Confirmed/Preparing orders; try cancelling In Transit or Completed. | Allowed cancellations set reason/status; stock is restored only when previously deducted; shipped/completed cancellation is refused. | High | |
| ADM-09 | Advance order status with invalid transition, missing courier/tracking, and courier/tracking text over 100 characters. | Invalid input is rejected without changing order status; valid transition requires shipping details for In Transit. | High | |
| ADM-10 | Mark Delivered COD order as paid; also try an order with a non-COD payment method (if available) and a non-Delivered order. | Only Delivered COD order becomes Paid/Completed. | High | |

## 10. Notifications and alerts

| ID | Steps | Expected result | Priority (High/Med/Low) | Pass/Fail |
|---|---|---|---|---|
| NOT-01 | As customer A, open notifications before and after an admin order confirmation/status update. | A receives relevant order notification; B cannot see A's notification. | High | |
| NOT-02 | Advance an order to Preparing, In Transit, and Delivered. | Customer receives the matching status notice at each successful transition. | Med | |
| NOT-03 | Have admin approve a reservation. | Reservation owner receives confirmation; another customer does not. | Med | |
| NOT-04 | As customer, mark all notifications read and refresh/reopen the bell. | Unread count becomes zero and read state persists. | Low | |
| NOT-05 | Create an active promotion and a new active product. | Only opted-in customers receive promotion notifications; the new-arrival notification is broadcast to customers. | Med | |
| NOT-06 | As admin, open notification bell/summary while pending orders/reservations or low-stock products exist. | Summary counts and links correspond to the listed records; removing the condition updates the summary. | Med | |
| NOT-07 | Cancel an order with a reason and inspect customer notification and admin recent-cancellation alert. | Customer is told the order was cancelled; admin alert shows the correct order/reason. | Med | |
| NOT-08 | Call customer notifications while logged out and attempt to mark read. | No private notifications are returned or changed. | High | |

## 11. Dashboard, sales, and reorder reports

| ID | Steps | Expected result | Priority (High/Med/Low) | Pass/Fail |
|---|---|---|---|---|
| RPT-01 | Open Dashboard while logged out/customer, then as admin. | First two are denied; admin dashboard loads. | High | |
| RPT-02 | Open Sales report with default dates and with a valid custom date range. | Report loads and totals correspond to included order statuses/date range. | Med | |
| RPT-03 | Set end date earlier than start date. | Request is rejected with a clear invalid-range response; no server error page. | Med | |
| RPT-04 | Filter sales by brand, category, and status, then page through transactions. | Each filter and page returns matching records and stable totals. | Med | |
| RPT-05 | Export the same sales range to PDF. | PDF downloads, opens, and shows the selected period and report totals/transactions. | Med | |
| RPT-06 | Open Reorder Points with each status and a product search; export the filtered result. | Report/filter/export use the selected status and search and do not include unrelated products. | Med | |
| RPT-07 | Restock an item at or above its reorder point and refresh admin alerts/report. | Low-stock/out-of-stock alert clears or updates according to the refreshed quantity. | Med | |
| RPT-08 | Try dashboard, sales data/export, and reorder report URLs as customer A. | Admin-only report data and files are not returned. | High | |

## 12. General validation, security, and responsive checks

| ID | Steps | Expected result | Priority (High/Med/Low) | Pass/Fail |
|---|---|---|---|---|
| GEN-01 | Submit a valid POST form after removing its anti-forgery token in browser developer tools (try login, address save, or checkout). | Request is rejected; no account/address/order/data change occurs. | High | |
| GEN-02 | Repeat a state-changing POST from an external/cross-site form without the app's token. | Anti-forgery validation rejects it; no state changes. | High | |
| GEN-03 | Enter quotes, ampersands, angle brackets, emoji, and non-English characters in allowed name/address/product/promotion fields. | Values validate and display safely; no markup/script executes and no data is corrupted. | High | |
| GEN-04 | Enter very long text in names, address, cancellation reason, search, and campaign fields. | Input is limited/validated or handled safely; no unhandled 500 error or silent corruption. | Med | |
| GEN-05 | Open unknown product, order, address, and reservation IDs. | User receives not-found/denied behavior without stack trace or private data. | Med | |
| GEN-06 | Trigger a recoverable invalid request and a controlled server-side error in a non-production environment. | User sees an understandable error page/message; no stack trace, connection string, or secret is exposed. | High | |
| GEN-07 | Navigate all major customer and admin pages at a narrow phone viewport; use forms, modals, cart, and order actions. | No critical controls are hidden/off-screen; forms remain usable and totals/status text are readable. | Med | |
| GEN-08 | Refresh pages after successful registration, address update, cart update, order placement, and status change. | Persisted values remain correct; browser refresh does not repeat state-changing operations. | High | |
| GEN-09 | Use browser Back/Forward across login, checkout, order confirmation, and logout. | Protected data remains inaccessible after logout; navigating back does not create a second order. | High | |
