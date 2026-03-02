using DataAccess.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Task.Areas.Member.ViewModels;


namespace Task.Areas.Member.Controllers
{
    [Area("Member")]
    public class AccountController : Controller
    {
        private readonly MemberAuthService _auth;

        public AccountController(MemberAuthService auth) => _auth = auth;

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        public async Task<IActionResult> Register(MemberRegisterVm vm)
        {
            if (!ModelState.IsValid) return View(vm);

            try
            {
                var id = await _auth.RegisterAsync(vm.FullName, vm.Email, vm.Password);

                await SignInMemberAsync(id, vm.Email, remember: true);

                return RedirectToAction("Index", "Home", new { area = "Member" });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(vm);
            }
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(MemberLoginVm vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var member = await _auth.LoginAsync(vm.Email, vm.Password);
            if (member == null)
            {
                ModelState.AddModelError("", "Invalid email or password.");
                return View(vm);
            }

            await SignInMemberAsync(member.Id, member.Email, vm.RememberMe);
            return RedirectToAction("Index", "Home", new { area = "Member" });
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("MemberCookie");
            return RedirectToAction("Login", new { area = "Member" });
        }

        private async ValueTask SignInMemberAsync(int memberId, string email, bool remember)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, memberId.ToString()),
                new Claim(ClaimTypes.Name, email),
                new Claim(ClaimTypes.Role, "Member")
            };

            var identity = new ClaimsIdentity(claims, "MemberCookie");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                "MemberCookie",
                principal,
                new AuthenticationProperties { IsPersistent = remember }
            );
        }
    }
}
