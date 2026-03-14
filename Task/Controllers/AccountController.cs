using System.Security.Claims;
using Core.Application.Common.Identity;
using DataAccess.Models.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Task.Areas.Member.ViewModels;

namespace Task.Controllers
{
    public class AccountController : Controller
    {
        private const string MemberScheme = "MemberCookie";
        private const string GoogleScheme = "Google";
        private const string MicrosoftScheme = "Microsoft";
        private readonly UserManager<ApplicationUser> _userMgr;
        private readonly SignInManager<ApplicationUser> _signInMgr;
        private readonly IMemberAuthService _memberAuth;

        public AccountController(
            UserManager<ApplicationUser> userMgr,
            SignInManager<ApplicationUser> signInMgr,
            IMemberAuthService memberAuth)
        {
            _userMgr = userMgr;
            _signInMgr = signInMgr;
            _memberAuth = memberAuth;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            await PopulateExternalProvidersAsync();
            return View(new UnifiedLoginVm { ReturnUrl = returnUrl });
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Register(string? returnUrl = null)
        {
            await PopulateExternalProvidersAsync();
            return View(new UnifiedRegisterVm { ReturnUrl = returnUrl });
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(UnifiedLoginVm vm)
        {
            if (!ModelState.IsValid)
            {
                await PopulateExternalProvidersAsync();
                return View(vm);
            }

            var email = vm.Email.Trim().ToLowerInvariant();

            // Ensure there is only one active identity after a new login attempt.
            await _signInMgr.SignOutAsync();
            await HttpContext.SignOutAsync(MemberScheme);

            var adminUser = await _userMgr.FindByEmailAsync(email);
            if (adminUser != null && await _userMgr.IsInRoleAsync(adminUser, "Admin"))
            {
                var adminPasswordOk = await _userMgr.CheckPasswordAsync(adminUser, vm.Password);
                if (adminPasswordOk)
                {
                    await _signInMgr.SignInAsync(adminUser, vm.RememberMe);
                    return RedirectAfterLogin(isAdmin: true, vm.ReturnUrl);
                }
            }

            var member = await _memberAuth.LoginAsync(email, vm.Password);
            if (member != null)
            {
                await SignInMemberAsync(member.Id, member.Email, vm.RememberMe);
                return RedirectAfterLogin(isAdmin: false, vm.ReturnUrl);
            }

            ModelState.AddModelError("", "Invalid email or password.");
            vm.Password = string.Empty;
            await PopulateExternalProvidersAsync();
            return View(vm);
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(UnifiedRegisterVm vm)
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
                ModelState.AddModelError(nameof(vm.Email), "This email is reserved for an admin account.");
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

                return RedirectAfterLogin(isAdmin: false, vm.ReturnUrl);
            }
            catch (Exception ex) when (ex.Message.Contains("Email already exists", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(vm.Email), "Email is already registered.");
            }
            catch
            {
                ModelState.AddModelError("", "Unable to create account right now.");
            }

            vm.Password = string.Empty;
            await PopulateExternalProvidersAsync();
            return View(vm);
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

            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
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
                TempData["LoginError"] = $"External login error: {remoteError}";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var info = await _signInMgr.GetExternalLoginInfoAsync();
            if (info == null)
            {
                TempData["LoginError"] = "Unable to load external login information.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var email = GetExternalEmail(info.Principal);
            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["LoginError"] = "No email returned from external provider.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            email = email.Trim().ToLowerInvariant();
            var displayName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email;

            await _signInMgr.SignOutAsync();
            await HttpContext.SignOutAsync(MemberScheme);

            var adminUser = await _userMgr.FindByEmailAsync(email);
            if (adminUser != null && await _userMgr.IsInRoleAsync(adminUser, "Admin"))
            {
                var existingLogins = await _userMgr.GetLoginsAsync(adminUser);
                var hasExternalLink = existingLogins.Any(l =>
                    l.LoginProvider == info.LoginProvider &&
                    l.ProviderKey == info.ProviderKey);

                if (!hasExternalLink)
                {
                    await _userMgr.AddLoginAsync(adminUser, info);
                }

                await _signInMgr.SignInAsync(adminUser, isPersistent: false);
                return RedirectAfterLogin(isAdmin: true, returnUrl);
            }

            var member = await _memberAuth.FindByEmailAsync(email);
            if (member != null && !member.IsActive)
            {
                TempData["LoginError"] = "Your account is blocked.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            member ??= await _memberAuth.RegisterExternalAsync(displayName, email);
            await SignInMemberAsync(member.Id, member.Email, remember: false);
            return RedirectAfterLogin(isAdmin: false, returnUrl);
        }

        [AcceptVerbs("GET", "POST")]
        public async Task<IActionResult> Logout()
        {
            await _signInMgr.SignOutAsync();
            await HttpContext.SignOutAsync(MemberScheme);
            return RedirectToAction(nameof(Login));
        }

        private IActionResult RedirectAfterLogin(bool isAdmin, string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                if (isAdmin && returnUrl.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase))
                    return LocalRedirect(returnUrl);

                if (!isAdmin && returnUrl.StartsWith("/Member", StringComparison.OrdinalIgnoreCase))
                    return LocalRedirect(returnUrl);
            }

            return isAdmin
                ? RedirectToAction("Index", "Dashboard", new { area = "Admin" })
                : RedirectToAction("Index", "Home", new { area = "Member" });
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
