using DataAccess.Models.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Task.Areas.Admin.ViewModels;
using Task.Contracts;
using MemberEntity = DataAccess.Models.Entities.Member;

namespace Task.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private const string AdminRole = "Admin";
        private readonly IRepository<MemberEntity> _members;
        private readonly UserManager<ApplicationUser> _userMgr;
        private readonly RoleManager<IdentityRole> _roleMgr;

        public UsersController(
            IRepository<MemberEntity> members,
            UserManager<ApplicationUser> userMgr,
            RoleManager<IdentityRole> roleMgr)
        {
            _members = members;
            _userMgr = userMgr;
            _roleMgr = roleMgr;
        }

        [HttpGet]
        public IActionResult Index() => View();

        [HttpGet]
        public async System.Threading.Tasks.Task<IActionResult> Admins()
        {
            var admins = await _userMgr.GetUsersInRoleAsync(AdminRole);
            return View(admins.OrderByDescending(x => x.CreatedAt).ToList());
        }

        [HttpGet]
        public IActionResult CreateAdmin() => View(new CreateAdminVm());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> CreateAdmin(CreateAdminVm vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            await EnsureAdminRoleAsync();

            var email = vm.Email.Trim().ToLowerInvariant();
            var existing = await _userMgr.FindByEmailAsync(email);

            if (existing != null)
            {
                if (!await _userMgr.IsInRoleAsync(existing, AdminRole))
                {
                    var addRoleRes = await _userMgr.AddToRoleAsync(existing, AdminRole);
                    if (!addRoleRes.Succeeded)
                    {
                        ModelState.AddModelError(string.Empty, string.Join(" | ", addRoleRes.Errors.Select(e => e.Description)));
                        return View(vm);
                    }
                }

                TempData["UserAction"] = "This email already exists and is now assigned as admin.";
                return RedirectToAction(nameof(Admins));
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = vm.FullName.Trim()
            };

            var createRes = await _userMgr.CreateAsync(user, vm.Password);
            if (!createRes.Succeeded)
            {
                foreach (var error in createRes.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View(vm);
            }

            var roleRes = await _userMgr.AddToRoleAsync(user, AdminRole);
            if (!roleRes.Succeeded)
            {
                TempData["UserAction"] = $"Admin created but role assignment failed: {string.Join(" | ", roleRes.Errors.Select(e => e.Description))}";
                return RedirectToAction(nameof(Admins));
            }

            TempData["UserAction"] = "New admin user created successfully.";
            return RedirectToAction(nameof(Admins));
        }

        [HttpGet]
        public async System.Threading.Tasks.Task<IActionResult> Members()
        {
            var admins = await _userMgr.GetUsersInRoleAsync(AdminRole);
            var adminEmails = admins
                .Select(x => x.Email?.Trim().ToLowerInvariant() ?? string.Empty)
                .ToHashSet();

            var allMembers = await _members.GetAllAsync();
            var regularMembers = allMembers
                .Where(x => !adminEmails.Contains(x.Email.Trim().ToLowerInvariant()))
                .OrderByDescending(x => x.CreatedAt)
                .ToList();

            return View(regularMembers);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> ToggleBlock(int id)
        {
            var user = await _members.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var email = user.Email.Trim().ToLowerInvariant();
            var adminUser = await _userMgr.FindByEmailAsync(email);
            if (adminUser != null && await _userMgr.IsInRoleAsync(adminUser, AdminRole))
            {
                TempData["UserAction"] = "Blocking admin accounts is disabled.";
                return RedirectToAction(nameof(Members));
            }

            user.IsActive = !user.IsActive;
            _members.Update(user);
            await _members.SaveChangesAsync();

            TempData["UserAction"] = user.IsActive
                ? "User has been unblocked."
                : "User has been blocked.";

            return RedirectToAction(nameof(Members));
        }

        private async System.Threading.Tasks.Task EnsureAdminRoleAsync()
        {
            if (!await _roleMgr.RoleExistsAsync(AdminRole))
            {
                await _roleMgr.CreateAsync(new IdentityRole(AdminRole));
            }
        }
    }
}
