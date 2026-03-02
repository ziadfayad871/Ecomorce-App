using Microsoft.AspNetCore.Mvc;
using Task.Areas.Member.ViewModels;

namespace Task.Areas.Member.Controllers
{
    [Area("Member")]
    public class AccountController : Controller
    {
        [HttpGet]
        public IActionResult Register() =>
            RedirectToAction("Login", "Account", new { area = "" });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(MemberRegisterVm vm) =>
            RedirectToAction("Login", "Account", new { area = "" });

        [HttpGet]
        public IActionResult Login(string? returnUrl = null) =>
            RedirectToAction("Login", "Account", new { area = "", returnUrl });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(MemberLoginVm vm) =>
            RedirectToAction("Login", "Account", new { area = "" });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout() =>
            RedirectToAction("Logout", "Account", new { area = "" });
    }
}
