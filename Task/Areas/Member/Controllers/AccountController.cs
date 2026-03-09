using System.Security.Claims;
using Core.Application.Common.Identity;
using DataAccess.Models.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Task.Areas.Member.ViewModels;

namespace Task.Areas.Member.Controllers
{
    [Area("Member")]
    public class AccountController : Controller
    {
        private const string MemberScheme = "MemberCookie";
        private const string GoogleScheme = "Google";
        private const string MicrosoftScheme = "Microsoft";
        private readonly IMemberAuthService _memberAuth;
        private readonly SignInManager<ApplicationUser> _signInMgr;
        private readonly UserManager<ApplicationUser> _userMgr;

        public AccountController(
            IMemberAuthService memberAuth,
            SignInManager<ApplicationUser> signInMgr,
            UserManager<ApplicationUser> userMgr)
        {
            _memberAuth = memberAuth;
            _signInMgr = signInMgr;
            _userMgr = userMgr;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Register(string? returnUrl = null)
        {
            await PopulateExternalProvidersAsync();
            ViewBag.ReturnUrl = returnUrl;
            return View(new MemberRegisterVm());
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(MemberRegisterVm vm)
        {
            if (!ModelState.IsValid)
            {
                await PopulateExternalProvidersAsync();
                return View(vm);
            }

            var fullName = vm.FullName.Trim();
            var email = vm.Email.Trim().ToLowerInvariant();
            var adminUser = await _userMgr.FindByEmailAsync(email);

            if (adminUser != null && await _userMgr.IsInRoleAsync(adminUser, "Admin"))
            {
                ModelState.AddModelError(nameof(vm.Email), "هذا البريد الإلكتروني مخصص لحساب إداري.");
                vm.Password = string.Empty;
                await PopulateExternalProvidersAsync();
                return View(vm);
            }

            try
            {
                var memberId = await _memberAuth.RegisterAsync(fullName, email, vm.Password);
                await _signInMgr.SignOutAsync();
                await HttpContext.SignOutAsync(MemberScheme);
                await SignInMemberAsync(memberId, email, remember: false);
                TempData["StoreSuccess"] = "تم إنشاء الحساب بنجاح.";
                return RedirectToAction("Index", "Home", new { area = "Member" });
            }
            catch (Exception ex) when (ex.Message.Contains("Email already exists", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(vm.Email), "هذا البريد الإلكتروني مستخدم بالفعل.");
            }
            catch
            {
                ModelState.AddModelError(string.Empty, "تعذر إنشاء الحساب حالياً. حاول مرة أخرى.");
            }

            vm.Password = string.Empty;
            await PopulateExternalProvidersAsync();
            return View(vm);
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            await PopulateExternalProvidersAsync();
            ViewBag.ReturnUrl = returnUrl;
            return View(new MemberLoginVm());
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(MemberLoginVm vm, string? returnUrl = null)
        {
            await PopulateExternalProvidersAsync();
            ViewBag.ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var email = vm.Email.Trim().ToLowerInvariant();

            await _signInMgr.SignOutAsync();
            await HttpContext.SignOutAsync(MemberScheme);

            var adminUser = await _userMgr.FindByEmailAsync(email);
            if (adminUser != null && await _userMgr.IsInRoleAsync(adminUser, "Admin"))
            {
                ModelState.AddModelError(string.Empty, "يرجى استخدام بوابة الإدارة لتسجيل دخول المشرفين.");
                vm.Password = string.Empty;
                return View(vm);
            }

            var member = await _memberAuth.LoginAsync(email, vm.Password);
            if (member == null)
            {
                ModelState.AddModelError(string.Empty, "البريد الإلكتروني أو كلمة المرور غير صحيحة.");
                vm.Password = string.Empty;
                return View(vm);
            }

            await SignInMemberAsync(member.Id, member.Email, vm.RememberMe);
            TempData["StoreSuccess"] = "تم تسجيل الدخول بنجاح.";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction("Index", "Home", new { area = "Member" });
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { area = "Member", returnUrl });
            if (string.IsNullOrWhiteSpace(redirectUrl))
            {
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var properties = _signInMgr.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
        {
            if (!string.IsNullOrWhiteSpace(remoteError))
            {
                TempData["LoginError"] = $"تعذر إتمام تسجيل الدخول الخارجي: {remoteError}";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var info = await _signInMgr.GetExternalLoginInfoAsync();
            if (info == null)
            {
                TempData["LoginError"] = "تعذر تحميل بيانات تسجيل الدخول الخارجي.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var email = GetExternalEmail(info.Principal);
            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["LoginError"] = "مزود تسجيل الدخول لم يرسل البريد الإلكتروني.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            email = email.Trim().ToLowerInvariant();
            var displayName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email;

            await _signInMgr.SignOutAsync();
            await HttpContext.SignOutAsync(MemberScheme);

            var adminUser = await _userMgr.FindByEmailAsync(email);
            if (adminUser != null && await _userMgr.IsInRoleAsync(adminUser, "Admin"))
            {
                TempData["LoginError"] = "هذا البريد مرتبط بحساب إداري. استخدم بوابة الإدارة.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var member = await _memberAuth.FindByEmailAsync(email);
            if (member != null && !member.IsActive)
            {
                TempData["LoginError"] = "تم إيقاف هذا الحساب.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            member ??= await _memberAuth.RegisterExternalAsync(displayName, email);
            await SignInMemberAsync(member.Id, member.Email, remember: false);

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            TempData["StoreSuccess"] = "تم تسجيل الدخول بنجاح.";
            return RedirectToAction("Index", "Home", new { area = "Member" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInMgr.SignOutAsync();
            await HttpContext.SignOutAsync(MemberScheme);
            return RedirectToAction(nameof(Login));
        }

        private async ValueTask SignInMemberAsync(int memberId, string email, bool remember)
        {
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
                new AuthenticationProperties { IsPersistent = remember });
        }

        private async System.Threading.Tasks.Task PopulateExternalProvidersAsync()
        {
            var providers = await _signInMgr.GetExternalAuthenticationSchemesAsync();
            var names = providers.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            ViewBag.GoogleEnabled = names.Contains(GoogleScheme);
            ViewBag.MicrosoftEnabled = names.Contains(MicrosoftScheme);
        }

        private static string? GetExternalEmail(ClaimsPrincipal principal) =>
            principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue("email")
            ?? principal.FindFirstValue("preferred_username");
    }
}
