using System.Security.Claims;
using DataAccess.Models.Identity;
using DataAccess.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Task.ViewModels;

namespace Task.Controllers
{
    public class AccountController : Controller
    {
        private const string MemberScheme = "MemberCookie";
        private readonly UserManager<ApplicationUser> _userMgr;
        private readonly SignInManager<ApplicationUser> _signInMgr;
        private readonly MemberAuthService _memberAuth;

        public AccountController(
            UserManager<ApplicationUser> userMgr,
            SignInManager<ApplicationUser> signInMgr,
            MemberAuthService memberAuth)
        {
            _userMgr = userMgr;
            _signInMgr = signInMgr;
            _memberAuth = memberAuth;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null) =>
            View(new UnifiedLoginVm { ReturnUrl = returnUrl });

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(UnifiedLoginVm vm)
        {
            if (!ModelState.IsValid) return View(vm);

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
            return View(vm);
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
    }
}
