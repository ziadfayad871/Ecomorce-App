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
        public IActionResult Login(string? returnUrl = null) =>
            RedirectToAction("Login", "Account", new { area = "", returnUrl });

        [HttpPost]
        public async Task<IActionResult> Login(AdminLoginVm vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var email = vm.Email.Trim().ToLower();
            var user = await _userMgr.FindByEmailAsync(email);

            if (user == null || !await _userMgr.IsInRoleAsync(user, "Admin"))
            {
                ModelState.AddModelError("", "Not Admin");
                return View(vm);
            }

            var res = await _signInMgr.PasswordSignInAsync(user, vm.Password, vm.RememberMe, false);
            if (!res.Succeeded)
            {
                ModelState.AddModelError("", "Wrong credentials");
                return View(vm);
            }

            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInMgr.SignOutAsync();
            await HttpContext.SignOutAsync("MemberCookie");
            return RedirectToAction("Login", "Account", new { area = "" });
        }
    }
}
