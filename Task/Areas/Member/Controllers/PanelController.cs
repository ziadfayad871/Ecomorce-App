using Core.Application.Members.Contracts;
using Core.Application.Members.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Task.Areas.Member.Controllers
{
    [Area("Member")]
    [Authorize(AuthenticationSchemes = MemberScheme)]
    public class PanelController : Controller
    {
        private const string MemberScheme = "MemberCookie";
        private readonly IMemberPanelService _memberPanelService;

        public PanelController(IMemberPanelService memberPanelService)
        {
            _memberPanelService = memberPanelService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var memberId = GetCurrentMemberId();
            if (!memberId.HasValue)
            {
                return RedirectToAction("Login", "Account", new { area = "Member" });
            }

            var cartItemsCount = HttpContext.Session.GetInt32("CartCount") ?? 0;
            var model = await _memberPanelService.BuildDashboardAsync(memberId.Value, cartItemsCount);
            if (model == null)
            {
                TempData["StoreError"] = "تعذر تحميل لوحة التحكم.";
                return RedirectToAction("Index", "Home", new { area = "Member" });
            }

            ViewData["Title"] = "لوحة التحكم";
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Orders()
        {
            var memberId = GetCurrentMemberId();
            if (!memberId.HasValue)
            {
                return RedirectToAction("Login", "Account", new { area = "Member" });
            }

            ViewData["Title"] = "طلباتي";
            var model = await _memberPanelService.GetOrdersAsync(memberId.Value);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> OrderDetails(int id)
        {
            var memberId = GetCurrentMemberId();
            if (!memberId.HasValue)
            {
                return RedirectToAction("Login", "Account", new { area = "Member" });
            }

            var model = await _memberPanelService.GetOrderDetailsAsync(memberId.Value, id);
            if (model == null)
            {
                TempData["StoreError"] = "الطلب غير موجود.";
                return RedirectToAction(nameof(Orders));
            }

            ViewData["Title"] = $"تفاصيل الطلب #{model.OrderNumber}";
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Favorites()
        {
            var memberId = GetCurrentMemberId();
            if (!memberId.HasValue)
            {
                return RedirectToAction("Login", "Account", new { area = "Member" });
            }

            ViewData["Title"] = "المنتجات المتابعة";
            var model = await _memberPanelService.GetFavoritesAsync(memberId.Value);
            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ToggleFavorite([FromForm] int productId, [FromForm] string? returnUrl = null)
        {
            var memberId = GetCurrentMemberId();
            if (!memberId.HasValue)
            {
                if (IsAjaxRequest())
                {
                    return Json(new
                    {
                        success = false,
                        requiresLogin = true,
                        message = "سجل الدخول أولاً لمتابعة المنتجات.",
                        redirectUrl = Url.Action("Login", "Account", new { area = "Member", returnUrl = "/Member/Home/Index" })
                    });
                }

                return RedirectToAction("Login", "Account", new { area = "Member", returnUrl = "/Member/Home/Index" });
            }

            var result = await _memberPanelService.ToggleFavoriteAsync(memberId.Value, productId);
            if (IsAjaxRequest())
            {
                return Json(new
                {
                    success = result.Success,
                    isTracked = result.IsTracked,
                    message = result.Message,
                    favoritesCount = result.FavoritesCount
                });
            }

            if (result.Success)
            {
                TempData["StoreSuccess"] = result.Message;
            }
            else
            {
                TempData["StoreError"] = result.Message;
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Favorites));
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var memberId = GetCurrentMemberId();
            if (!memberId.HasValue)
            {
                return RedirectToAction("Login", "Account", new { area = "Member" });
            }

            var model = await _memberPanelService.GetProfileAsync(memberId.Value);
            if (model == null)
            {
                TempData["StoreError"] = "الحساب غير موجود.";
                return RedirectToAction(nameof(Index));
            }

            ViewData["Title"] = "الملف الشخصي";
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(MemberProfileVm model)
        {
            var memberId = GetCurrentMemberId();
            if (!memberId.HasValue)
            {
                return RedirectToAction("Login", "Account", new { area = "Member" });
            }

            ViewData["Title"] = "الملف الشخصي";

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _memberPanelService.UpdateProfileAsync(memberId.Value, model.FullName, model.Email, model.NewPassword);
            if (!result.Success || !result.MemberId.HasValue || string.IsNullOrWhiteSpace(result.MemberEmail))
            {
                TempData["StoreError"] = result.Message;
                model.CreatedAt = model.CreatedAt == default ? DateTime.UtcNow : model.CreatedAt;
                return View(model);
            }

            await SignInMemberAsync(result.MemberId.Value, result.MemberEmail);
            TempData["StoreSuccess"] = result.Message;
            return RedirectToAction(nameof(Profile));
        }

        private int? GetCurrentMemberId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var id) ? id : null;
        }

        private bool IsAjaxRequest()
        {
            if (!Request.Headers.TryGetValue("X-Requested-With", out var headerValue))
            {
                return false;
            }

            return string.Equals(headerValue.ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
        }

        private async ValueTask SignInMemberAsync(int memberId, string email)
        {
            await HttpContext.SignOutAsync(MemberScheme);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, memberId.ToString()),
                new Claim(ClaimTypes.Name, email),
                new Claim(ClaimTypes.Role, "Member")
            };

            var identity = new ClaimsIdentity(claims, MemberScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                MemberScheme,
                principal,
                new AuthenticationProperties { IsPersistent = true });
        }
    }
}

