using Core.Application.Common.Activities;
using DataAccess.Models.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Task.Areas.Admin.ViewModels;

namespace Task.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class SettingsController : Controller
    {
        private readonly UserManager<ApplicationUser> _userMgr;
        private readonly SignInManager<ApplicationUser> _signInMgr;
        private readonly IAdminActivityService _activity;

        public SettingsController(
            UserManager<ApplicationUser> userMgr,
            SignInManager<ApplicationUser> signInMgr,
            IAdminActivityService activity)
        {
            _userMgr = userMgr;
            _signInMgr = signInMgr;
            _activity = activity;
        }

        [HttpGet]
        public IActionResult Language()
        {
            var current = Request.Cookies["Admin.Language"];
            var selected = string.Equals(current, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "ar";
            return View(new LanguageSettingsVm { SelectedLanguage = selected });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Language(LanguageSettingsVm vm)
        {
            var selected = string.Equals(vm.SelectedLanguage, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "ar";
            Response.Cookies.Append("Admin.Language", selected, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                HttpOnly = false
            });

            TempData["SettingsAction"] = "تم حفظ إعدادات اللغة بنجاح.";
            _activity.Add("الإعدادات", $"تم تحديث إعدادات اللغة إلى {(selected == "en" ? "English" : "العربية") }.");
            return RedirectToAction(nameof(Language));
        }

        [HttpGet]
        public async System.Threading.Tasks.Task<IActionResult> Account()
        {
            var user = await _userMgr.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            return View(new AccountSettingsVm
            {
                FullName = user.FullName ?? string.Empty,
                Email = user.Email ?? string.Empty
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> Account(AccountSettingsVm vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var user = await _userMgr.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var normalizedEmail = vm.Email.Trim().ToLowerInvariant();
            var existingEmailOwner = await _userMgr.FindByEmailAsync(normalizedEmail);
            if (existingEmailOwner != null && !string.Equals(existingEmailOwner.Id, user.Id, StringComparison.Ordinal))
            {
                ModelState.AddModelError(nameof(vm.Email), "هذا البريد مستخدم بالفعل.");
                return View(vm);
            }

            user.FullName = vm.FullName.Trim();
            user.Email = normalizedEmail;
            user.UserName = normalizedEmail;

            var updateRes = await _userMgr.UpdateAsync(user);
            if (!updateRes.Succeeded)
            {
                foreach (var error in updateRes.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View(vm);
            }

            if (!string.IsNullOrWhiteSpace(vm.NewPassword))
            {
                var token = await _userMgr.GeneratePasswordResetTokenAsync(user);
                var resetRes = await _userMgr.ResetPasswordAsync(user, token, vm.NewPassword);
                if (!resetRes.Succeeded)
                {
                    foreach (var error in resetRes.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }

                    return View(vm);
                }
            }

            await _signInMgr.RefreshSignInAsync(user);
            TempData["SettingsAction"] = "تم تحديث بيانات الحساب بنجاح.";
            _activity.Add("الإعدادات", "تم تحديث بيانات الحساب.");
            return RedirectToAction(nameof(Account));
        }
    }
}
