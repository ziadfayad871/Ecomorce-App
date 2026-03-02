using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Task.Areas.Member.ViewModels;
using Task.Contracts;
using Task.Helpers;

namespace Task.Areas.Member.Controllers
{
    [Area("Member")]
    public class HomeController : Controller
    {
        private const string MemberScheme = "MemberCookie";
        private const string CartSessionKey = "CartLines";
        private const string CartCountSessionKey = "CartCount";
        private const string StripeCheckoutEndpoint = "https://api.stripe.com/v1/checkout/sessions";

        private readonly IProductRepository _products;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;

        public HomeController(
            IProductRepository products,
            IHttpClientFactory httpClientFactory,
            IConfiguration config)
        {
            _products = products;
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? categoryId = null)
        {
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

            var items = categoryId.HasValue
                ? allItems.Where(p => p.CategoryId == categoryId.Value).ToList()
                : allItems;

            ViewBag.Categories = categories;
            ViewBag.SelectedCategoryId = categoryId;
            ViewBag.SelectedCategoryName = categoryId.HasValue
                ? categories.FirstOrDefault(c => c.Id == categoryId.Value)?.Name ?? "المنتجات"
                : "كل المنتجات";

            return View(items);
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
                        CategoryName = p.Category?.Name ?? "Clothing",
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
        public async Task<IActionResult> AddToCart(int productId, string? returnUrl = null)
        {
            var auth = await HttpContext.AuthenticateAsync(MemberScheme);
            if (!auth.Succeeded)
            {
                return RedirectToAction("Login", "Account", new
                {
                    area = "",
                    returnUrl = "/Member/Home/Index"
                });
            }

            var p = await _products.GetByIdAsync(productId);
            if (p == null)
            {
                TempData["StoreError"] = "Product not found.";
                return RedirectToAction(nameof(Index));
            }

            var lines = GetCartLines();
            var current = lines.FirstOrDefault(x => x.ProductId == productId);
            if (current == null)
            {
                lines.Add(new CartLineVm { ProductId = productId, Quantity = 1 });
            }
            else
            {
                current.Quantity++;
            }

            SaveCartLines(lines);
            TempData["StoreSuccess"] = "Added to cart.";

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
                TempData["StoreError"] = "Cart is empty.";
                return RedirectToAction(nameof(Cart));
            }

            var stripeSecretKey = _config["Stripe:SecretKey"];
            if (string.IsNullOrWhiteSpace(stripeSecretKey) || stripeSecretKey.Contains("REPLACE_WITH", StringComparison.OrdinalIgnoreCase))
            {
                TempData["StoreError"] = "Stripe key is missing in appsettings. Add your Stripe Secret Key first.";
                return RedirectToAction(nameof(Cart));
            }

            var currency = (_config["Stripe:Currency"] ?? "egp").ToLowerInvariant();
            var products = await _products.GetAllWithCategoryAndImagesAsync();
            var productMap = products
                .Where(p => lines.Any(x => x.ProductId == p.Id))
                .ToDictionary(p => p.Id);

            if (productMap.Count == 0)
            {
                TempData["StoreError"] = "No valid products found for checkout.";
                return RedirectToAction(nameof(Cart));
            }

            var successUrl = Url.Action(nameof(PaymentSuccess), "Home", new { area = "Member" }, Request.Scheme);
            var cancelUrl = Url.Action(nameof(Cart), "Home", new { area = "Member" }, Request.Scheme);
            if (string.IsNullOrWhiteSpace(successUrl) || string.IsNullOrWhiteSpace(cancelUrl))
            {
                TempData["StoreError"] = "Failed to build checkout URLs.";
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
            foreach (var line in lines)
            {
                if (!productMap.TryGetValue(line.ProductId, out var product))
                {
                    continue;
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
                index++;
            }

            if (index == 0)
            {
                TempData["StoreError"] = "Cart contains invalid data.";
                return RedirectToAction(nameof(Cart));
            }

            var client = _httpClientFactory.CreateClient();
            using var req = new HttpRequestMessage(HttpMethod.Post, StripeCheckoutEndpoint);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", stripeSecretKey);
            req.Content = new FormUrlEncodedContent(form);

            using var res = await client.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                TempData["StoreError"] = ExtractStripeError(body) ?? "Stripe checkout failed.";
                return RedirectToAction(nameof(Cart));
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("url", out var urlElement))
            {
                TempData["StoreError"] = "Stripe response is missing checkout URL.";
                return RedirectToAction(nameof(Cart));
            }

            var checkoutUrl = urlElement.GetString();
            if (string.IsNullOrWhiteSpace(checkoutUrl))
            {
                TempData["StoreError"] = "Stripe checkout URL is empty.";
                return RedirectToAction(nameof(Cart));
            }

            return Redirect(checkoutUrl);
        }

        [Authorize(AuthenticationSchemes = MemberScheme)]
        [HttpGet]
        public IActionResult PaymentSuccess(string? session_id = null)
        {
            var hasSession = !string.IsNullOrWhiteSpace(session_id);
            if (hasSession)
            {
                ClearCart();
            }

            ViewBag.HasStripeSession = hasSession;
            ViewBag.StripeSessionId = session_id;
            return View();
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
