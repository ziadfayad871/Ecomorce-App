using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Core.Application.Catalog.Contracts;
using Core.Application.Common.Activities;
using Core.Application.Common.Persistence;
using Core.Application.Members.Contracts;
using Core.Domain.Common;
using Core.Domain.Orders;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Task.Areas.Member.ViewModels;
using Task.Helpers;
using OrderEntity = Core.Domain.Entities.Order;
using OrderItemEntity = Core.Domain.Entities.OrderItem;
using MemberEntity = Core.Domain.Entities.Member;

namespace Task.Areas.Member.Controllers
{
    [Area("Member")]
    public class HomeController : Controller
    {
        private const string MemberScheme = "MemberCookie";
        private const string CartSessionKey = "CartLines";
        private const string CartCountSessionKey = "CartCount";
        private const string LastCheckoutTotalSessionKey = "LastCheckoutTotal";
        private const string StripeCheckoutEndpoint = "https://api.stripe.com/v1/checkout/sessions";

        private readonly IProductRepository _products;
        private readonly IRepository<OrderEntity> _orders;
        private readonly IRepository<OrderItemEntity> _orderItems;
        private readonly IRepository<MemberEntity> _members;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly IAdminActivityService _activity;
        private readonly IMemberPanelService _memberPanelService;
        private static readonly string[] AllSizes = { "S", "M", "L", "XL", "XXL", "XXXL" };
        private static readonly string[] ProductColors = { "أبيض", "أسود", "أزرق", "زيتي", "بيج", "رمادي", "خمري" };

        public HomeController(
            IProductRepository products,
            IRepository<OrderEntity> orders,
            IRepository<OrderItemEntity> orderItems,
            IRepository<MemberEntity> members,
            IHttpClientFactory httpClientFactory,
            IConfiguration config,
            IAdminActivityService activity,
            IMemberPanelService memberPanelService)
        {
            _products = products;
            _orders = orders;
            _orderItems = orderItems;
            _members = members;
            _httpClientFactory = httpClientFactory;
            _config = config;
            _activity = activity;
            _memberPanelService = memberPanelService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? categoryId = null, string? searchTerm = null, int page = 1, int pageSize = 8)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 24);

            var allItems = await _products.GetAllWithCategoryAndImagesAsync();

            var categories = allItems
                .Where(p => p.Category != null)
                .GroupBy(p => new { p.CategoryId, p.Category!.Name })
                .Select(g => new StoreCategoryFilterVm
                {
                    Id = g.Key.CategoryId,
                    Name = g.Key.Name,
                    Count = g.Count()
                })
                .OrderBy(c => c.Name)
                .ToList();

            var filteredItems = categoryId.HasValue
                ? allItems.Where(p => p.CategoryId == categoryId.Value)
                : allItems.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var normalizedSearch = searchTerm.Trim();
                filteredItems = filteredItems.Where(p =>
                    p.Name.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    (p.Category != null && p.Category.Name.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)));
            }

            var orderedItems = filteredItems
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .ToList();

            var totalCount = orderedItems.Count;
            var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
            page = Math.Min(page, totalPages);

            var items = orderedItems
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var favoriteIds = new HashSet<int>();
            var memberId = GetCurrentMemberId();
            if (memberId.HasValue)
            {
                favoriteIds = await _memberPanelService.GetFavoriteProductIdsAsync(memberId.Value);
            }

            ViewBag.Categories = categories;
            ViewBag.SelectedCategoryId = categoryId;
            ViewBag.SelectedCategoryName = categoryId.HasValue
                ? categories.FirstOrDefault(c => c.Id == categoryId.Value)?.Name ?? "تسوّق"
                : "كل المنتجات";
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;
            ViewBag.SearchTerm = searchTerm ?? string.Empty;
            ViewBag.FavoriteProductIds = favoriteIds;

            if (IsAjaxRequest())
            {
                return Json(new
                {
                    products = items.Select(x => MapProductForCard(x, favoriteIds.Contains(x.Id))),
                    currentPage = page,
                    totalPages,
                    totalCount
                });
            }

            return View(items);
        }

        [HttpGet]
        public IActionResult About()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Contact()
        {
            return View(new ContactFormVm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Contact(ContactFormVm model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            TempData["ContactSuccess"] = "تم إرسال رسالتك بنجاح. سنرد عليك في أقرب وقت.";
            return RedirectToAction(nameof(Contact));
        }

        [Authorize(AuthenticationSchemes = MemberScheme)]
        [HttpGet]
        public async Task<IActionResult> Cart()
        {
            var lines = GetCartLines();
            if (lines.Count == 0)
            {
                return View(new List<CartItemVm>());
            }

            var ids = lines.Select(x => x.ProductId).Distinct().ToHashSet();
            var products = await _products.GetAllWithCategoryAndImagesAsync();

            var model = products
                .Where(p => ids.Contains(p.Id))
                .Select(p =>
                {
                    var qty = lines.FirstOrDefault(x => x.ProductId == p.Id)?.Quantity ?? 1;
                    return new CartItemVm
                    {
                        ProductId = p.Id,
                        Name = p.Name,
                        CategoryName = p.Category?.Name ?? "قسم",
                        ImagePath = p.Images.FirstOrDefault()?.Path,
                        UnitPrice = p.Price,
                        Quantity = qty
                    };
                })
                .OrderByDescending(x => x.ProductId)
                .ToList();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1, string? size = null, string? returnUrl = null)
        {
            var auth = await HttpContext.AuthenticateAsync(MemberScheme);
            if (!auth.Succeeded)
            {
                if (IsAjaxRequest())
                {
                    return Json(new
                    {
                        success = false,
                        requiresLogin = true,
                        message = "يجب تسجيل الدخول أولاً لإضافة منتجات إلى العربة.",
                        redirectUrl = Url.Action("Login", "Account", new
                        {
                            area = "Member",
                            returnUrl = "/Member/Home/Index"
                        })
                    });
                }

                return RedirectToAction("Login", "Account", new
                {
                    area = "Member",
                    returnUrl = "/Member/Home/Index"
                });
            }

            var p = await _products.GetByIdAsync(productId);
            if (p == null)
            {
                if (IsAjaxRequest())
                {
                    return Json(new { success = false, message = "المنتج غير موجود." });
                }

                TempData["StoreError"] = "المنتج غير موجود.";
                return RedirectToAction(nameof(Index));
            }

            quantity = Math.Clamp(quantity, 1, 10);
            var lines = GetCartLines();
            var current = lines.FirstOrDefault(x => x.ProductId == productId);
            if (current == null)
            {
                lines.Add(new CartLineVm { ProductId = productId, Quantity = quantity });
            }
            else
            {
                current.Quantity += quantity;
            }

            SaveCartLines(lines);
            var successMessage = quantity > 1
                ? $"تمت إضافة {quantity} قطع إلى العربة."
                : "تمت إضافة المنتج إلى العربة.";

            if (IsAjaxRequest())
            {
                return Json(new
                {
                    success = true,
                    message = successMessage,
                    cartCount = HttpContext.Session.GetInt32(CartCountSessionKey) ?? 0,
                    selectedSize = size
                });
            }

            TempData["StoreSuccess"] = successMessage;

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(AuthenticationSchemes = MemberScheme)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveFromCart(int productId)
        {
            var lines = GetCartLines();
            var current = lines.FirstOrDefault(x => x.ProductId == productId);
            if (current != null)
            {
                lines.Remove(current);
                SaveCartLines(lines);
            }

            return RedirectToAction(nameof(Cart));
        }

        [Authorize(AuthenticationSchemes = MemberScheme)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult IncreaseQuantity(int productId)
        {
            var lines = GetCartLines();
            var current = lines.FirstOrDefault(x => x.ProductId == productId);
            if (current != null)
            {
                current.Quantity++;
                SaveCartLines(lines);
            }

            return RedirectToAction(nameof(Cart));
        }

        [Authorize(AuthenticationSchemes = MemberScheme)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DecreaseQuantity(int productId)
        {
            var lines = GetCartLines();
            var current = lines.FirstOrDefault(x => x.ProductId == productId);
            if (current != null)
            {
                current.Quantity--;
                if (current.Quantity <= 0)
                {
                    lines.Remove(current);
                }

                SaveCartLines(lines);
            }

            return RedirectToAction(nameof(Cart));
        }

        [Authorize(AuthenticationSchemes = MemberScheme)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout()
        {
            var lines = GetCartLines();
            if (lines.Count == 0)
            {
                TempData["StoreError"] = "العربة فارغة حالياً.";
                return RedirectToAction(nameof(Cart));
            }

            var stripeSecretKey = _config["Stripe:SecretKey"];
            if (string.IsNullOrWhiteSpace(stripeSecretKey) || stripeSecretKey.Contains("REPLACE_WITH", StringComparison.OrdinalIgnoreCase))
            {
                TempData["StoreError"] = "مفتاح Stripe غير مضبوط في الإعدادات. أضف المفتاح السري أولاً.";
                return RedirectToAction(nameof(Cart));
            }

            var currency = (_config["Stripe:Currency"] ?? "egp").ToLowerInvariant();
            var products = await _products.GetAllWithCategoryAndImagesAsync();
            var productMap = products
                .Where(p => lines.Any(x => x.ProductId == p.Id))
                .ToDictionary(p => p.Id);

            if (productMap.Count == 0)
            {
                TempData["StoreError"] = "لا توجد منتجات صالحة لإتمام الدفع.";
                return RedirectToAction(nameof(Cart));
            }

            var successUrl = Url.Action(nameof(PaymentSuccess), "Home", new { area = "Member" }, Request.Scheme);
            var cancelUrl = Url.Action(nameof(Cart), "Home", new { area = "Member" }, Request.Scheme);
            if (string.IsNullOrWhiteSpace(successUrl) || string.IsNullOrWhiteSpace(cancelUrl))
            {
                TempData["StoreError"] = "تعذر إنشاء روابط الدفع.";
                return RedirectToAction(nameof(Cart));
            }

            var form = new List<KeyValuePair<string, string>>
            {
                new("mode", "payment"),
                new("success_url", $"{successUrl}?session_id={{CHECKOUT_SESSION_ID}}"),
                new("cancel_url", cancelUrl),
                new("payment_method_types[0]", "card"),
                new("locale", "auto")
            };

            var index = 0;
            decimal checkoutTotal = 0m;
            foreach (var line in lines)
            {
                if (!productMap.TryGetValue(line.ProductId, out var product))
                {
                    continue;
                }

                if (product.StockQuantity < line.Quantity)
                {
                    TempData["StoreError"] = $"المنتج {product.Name} غير متوفر بالكمية المطلوبة.";
                    return RedirectToAction(nameof(Cart));
                }

                var unitAmount = (long)Math.Round(product.Price * 100m, MidpointRounding.AwayFromZero);
                if (unitAmount <= 0)
                {
                    continue;
                }

                form.Add(new KeyValuePair<string, string>($"line_items[{index}][price_data][currency]", currency));
                form.Add(new KeyValuePair<string, string>($"line_items[{index}][price_data][unit_amount]", unitAmount.ToString(CultureInfo.InvariantCulture)));
                form.Add(new KeyValuePair<string, string>($"line_items[{index}][price_data][product_data][name]", product.Name));
                form.Add(new KeyValuePair<string, string>($"line_items[{index}][quantity]", line.Quantity.ToString(CultureInfo.InvariantCulture)));
                checkoutTotal += product.Price * line.Quantity;
                index++;
            }

            if (index == 0)
            {
                TempData["StoreError"] = "العربة تحتوي على بيانات غير صالحة.";
                return RedirectToAction(nameof(Cart));
            }

            HttpContext.Session.SetString(LastCheckoutTotalSessionKey, checkoutTotal.ToString(CultureInfo.InvariantCulture));
            _activity.Add("الطلبات", $"تم إنشاء طلب جديد بقيمة {checkoutTotal:0.00} EGP (بانتظار الدفع).");

            var client = _httpClientFactory.CreateClient();
            using var req = new HttpRequestMessage(HttpMethod.Post, StripeCheckoutEndpoint);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", stripeSecretKey);
            req.Content = new FormUrlEncodedContent(form);

            using var res = await client.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                TempData["StoreError"] = ExtractStripeError(body) ?? "فشل إنشاء جلسة الدفع عبر Stripe.";
                return RedirectToAction(nameof(Cart));
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("url", out var urlElement))
            {
                TempData["StoreError"] = "استجابة Stripe لا تحتوي على رابط الدفع.";
                return RedirectToAction(nameof(Cart));
            }

            var checkoutUrl = urlElement.GetString();
            if (string.IsNullOrWhiteSpace(checkoutUrl))
            {
                TempData["StoreError"] = "رابط الدفع من Stripe فارغ.";
                return RedirectToAction(nameof(Cart));
            }

            string? stripeSessionId = null;
            if (doc.RootElement.TryGetProperty("id", out var idElement))
            {
                stripeSessionId = idElement.GetString();
            }

            var memberId = GetCurrentMemberId();
            var memberEmail = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? string.Empty;
            var memberName = await ResolveCurrentMemberNameAsync(memberId, memberEmail);

            var order = new OrderEntity
            {
                OrderNumber = GenerateOrderNumber(),
                MemberId = memberId,
                CustomerName = memberName,
                CustomerEmail = string.IsNullOrWhiteSpace(memberEmail) ? "member@store.local" : memberEmail.Trim(),
                TotalAmount = Math.Round(checkoutTotal, 2),
                Status = OrderStatusCatalog.Pending,
                PaymentSessionId = stripeSessionId,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _orders.AddAsync(order);
            await _orders.SaveChangesAsync();

            foreach (var line in lines)
            {
                if (!productMap.TryGetValue(line.ProductId, out var lineProduct))
                {
                    continue;
                }

                await _orderItems.AddAsync(new OrderItemEntity
                {
                    OrderId = order.Id,
                    ProductId = lineProduct.Id,
                    ProductName = lineProduct.Name,
                    UnitPrice = lineProduct.Price,
                    Quantity = line.Quantity
                });
            }
            await _orderItems.SaveChangesAsync();

            _activity.Add("الطلبات", $"طلب جديد #{order.OrderNumber} للعميل {order.CustomerName} بقيمة {order.TotalAmount:0.00} EGP.");

            return Redirect(checkoutUrl);
        }

        [Authorize(AuthenticationSchemes = MemberScheme)]
        [HttpGet]
        public async Task<IActionResult> PaymentSuccess(string? session_id = null)
        {
            var hasSession = !string.IsNullOrWhiteSpace(session_id);
            if (hasSession)
            {
                var checkoutTotal = 0m;
                var checkoutTotalRaw = HttpContext.Session.GetString(LastCheckoutTotalSessionKey);
                if (!string.IsNullOrWhiteSpace(checkoutTotalRaw))
                {
                    decimal.TryParse(checkoutTotalRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out checkoutTotal);
                }

                var order = (await _orders.FindAsync(x => x.PaymentSessionId == session_id)).FirstOrDefault();
                if (order != null)
                {
                    if (!string.Equals(order.Status, OrderStatusCatalog.Paid, StringComparison.OrdinalIgnoreCase))
                    {
                        order.Status = OrderStatusCatalog.Paid;
                        order.PaidAtUtc = DateTime.UtcNow;
                        _orders.Update(order);
                        await _orders.SaveChangesAsync();

                        var orderLines = await _orderItems.FindAsync(x => x.OrderId == order.Id);
                        foreach (var line in orderLines)
                        {
                            var product = await _products.GetByIdAsync(line.ProductId);
                            if (product == null)
                            {
                                continue;
                            }

                            product.StockQuantity = Math.Max(0, product.StockQuantity - line.Quantity);
                            _products.Update(product);
                        }
                        await _products.SaveChangesAsync();
                    }

                    _activity.Add("الطلبات", $"تم تأكيد بيع الطلب #{order.OrderNumber} للعميل {order.CustomerName} بقيمة {order.TotalAmount:0.00} EGP.");
                }
                else
                {
                    var orderMessage = checkoutTotal > 0m
                        ? $"تم تأكيد طلب مدفوع بقيمة {checkoutTotal:0.00} EGP."
                        : "تم تأكيد طلب مدفوع جديد.";
                    _activity.Add("الطلبات", orderMessage);
                }

                HttpContext.Session.Remove(LastCheckoutTotalSessionKey);
                ClearCart();
            }

            ViewBag.HasStripeSession = hasSession;
            ViewBag.StripeSessionId = session_id;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetProductDetails(int productId)
        {
            var product = await _products.GetWithCategoryAndImagesAsync(productId);
            if (product == null)
            {
                return Json(new
                {
                    success = false,
                    message = "لم يتم العثور على المنتج."
                });
            }

            return Json(new
            {
                success = true,
                product = new
                {
                    productName = product.Name,
                    productPrice = product.Price,
                    productRating = BuildRating(product.Id),
                    imgUrl = product.Images.FirstOrDefault()?.Path ?? "",
                    color = BuildColor(product.Id),
                    description = BuildDescription(product),
                    availableSizes = BuildAvailableSizes(product.Id)
                }
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetProductSizes(int productId)
        {
            var product = await _products.GetByIdAsync(productId);
            if (product == null)
            {
                return Json(new
                {
                    success = false,
                    message = "المنتج غير موجود."
                });
            }

            return Json(new
            {
                success = true,
                sizes = BuildAvailableSizes(product.Id)
            });
        }

        private static string? ExtractStripeError(string? rawBody)
        {
            if (string.IsNullOrWhiteSpace(rawBody))
            {
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(rawBody);
                if (doc.RootElement.TryGetProperty("error", out var error) &&
                    error.TryGetProperty("message", out var message))
                {
                    return message.GetString();
                }
            }
            catch
            {
                // Ignore parse errors and return null for fallback message.
            }

            return null;
        }

        private bool IsAjaxRequest()
        {
            if (!Request.Headers.TryGetValue("X-Requested-With", out var headerValue))
            {
                return false;
            }

            return string.Equals(headerValue.ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
        }

        private static object MapProductForCard(Core.Domain.Entities.Product product, bool isTracked)
        {
            return new
            {
                productId = product.Id,
                productName = product.Name,
                productPrice = product.Price,
                productRating = BuildRating(product.Id),
                brandName = product.Category?.Name ?? StoreBranding.BrandName,
                imgUrl = product.Images.FirstOrDefault()?.Path ?? "",
                isFeatured = product.Id % 2 == 0,
                isTracked
            };
        }

        private static int BuildRating(int productId) => (productId % 5) + 1;

        private static string BuildColor(int productId) => ProductColors[productId % ProductColors.Length];

        private static string[] BuildAvailableSizes(int productId)
        {
            var takeCount = 3 + (productId % 4); // 3..6 sizes
            return AllSizes.Take(takeCount).ToArray();
        }

        private static string BuildDescription(Core.Domain.Entities.Product product)
        {
            var categoryName = product.Category?.Name ?? "قسم المتجر";
            return $"منتج {categoryName} بخامة مريحة وتصميم عصري مناسب للاستخدام اليومي.";
        }

        private int? GetCurrentMemberId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var id) ? id : null;
        }

        private async Task<string> ResolveCurrentMemberNameAsync(int? memberId, string? memberEmail)
        {
            if (memberId.HasValue)
            {
                var member = await _members.GetByIdAsync(memberId.Value);
                if (member != null && !string.IsNullOrWhiteSpace(member.FullName))
                {
                    return member.FullName.Trim();
                }
            }

            if (!string.IsNullOrWhiteSpace(memberEmail))
            {
                var byEmail = (await _members.FindAsync(x => x.Email == memberEmail)).FirstOrDefault();
                if (byEmail != null && !string.IsNullOrWhiteSpace(byEmail.FullName))
                {
                    return byEmail.FullName.Trim();
                }
            }

            return "عميل";
        }

        private static string GenerateOrderNumber()
        {
            var random = Random.Shared.Next(1000, 9999);
            return $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}-{random}";
        }

        private List<CartLineVm> GetCartLines() =>
            HttpContext.Session.GetObject<List<CartLineVm>>(CartSessionKey) ?? new List<CartLineVm>();

        private void SaveCartLines(List<CartLineVm> lines)
        {
            HttpContext.Session.SetObject(CartSessionKey, lines);
            HttpContext.Session.SetInt32(CartCountSessionKey, lines.Sum(x => x.Quantity));
        }

        private void ClearCart()
        {
            HttpContext.Session.Remove(CartSessionKey);
            HttpContext.Session.SetInt32(CartCountSessionKey, 0);
        }
    }
}



