using DataAccess.Models.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Task.Areas.Admin.ViewModels;

namespace Task.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userMgr;
        private readonly SignInManager<ApplicationUser> _signInMgr;

        public AccountController(UserManager<ApplicationUser> userMgr, SignInManager<ApplicationUser> signInMgr)
        {
            _userMgr = userMgr;
            _signInMgr = signInMgr;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View(new AdminLoginVm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(AdminLoginVm vm, string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;

            if (!ModelState.IsValid) return View(vm);

            var email = vm.Email.Trim().ToLowerInvariant();
            var user = await _userMgr.FindByEmailAsync(email);

            if (user == null || !await _userMgr.IsInRoleAsync(user, "Admin"))
            {
                ModelState.AddModelError(string.Empty, "غير مسموح بالدخول من هذه البوابة.");
                vm.Password = string.Empty;
                return View(vm);
            }

            var res = await _signInMgr.PasswordSignInAsync(user, vm.Password, vm.RememberMe, false);
            if (!res.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "البريد الإلكتروني أو كلمة المرور غير صحيحة.");
                vm.Password = string.Empty;
                return View(vm);
            }

            TempData["DashboardAction"] = "تم تسجيل الدخول بنجاح.";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInMgr.SignOutAsync();
            await HttpContext.SignOutAsync("MemberCookie");
            return RedirectToAction(nameof(Login));
        }
    }
}
