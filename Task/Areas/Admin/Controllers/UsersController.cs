using DataAccess.Models.Identity;
using DataAccess.Services;
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
        private readonly MemberAuthService _memberAuth;

        public UsersController(
            IRepository<MemberEntity> members,
            UserManager<ApplicationUser> userMgr,
            RoleManager<IdentityRole> roleMgr,
            MemberAuthService memberAuth)
        {
            _members = members;
            _userMgr = userMgr;
            _roleMgr = roleMgr;
            _memberAuth = memberAuth;
        }

        [HttpGet]
        public async System.Threading.Tasks.Task<IActionResult> Index()
        {
            return View(await BuildPageVmAsync());
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
                return RedirectToAction(nameof(Index));
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
                return RedirectToAction(nameof(Index));
            }

            TempData["UserAction"] = "New admin user created successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult CreateMember() => View(new CreateMemberVm());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> CreateMember(CreateMemberVm vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            try
            {
                await _memberAuth.RegisterAsync(vm.FullName.Trim(), vm.Email.Trim(), vm.Password);
                TempData["UserAction"] = "New member user created successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(vm);
            }
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

            user.IsActive = !user.IsActive;
            _members.Update(user);
            await _members.SaveChangesAsync();

            TempData["UserAction"] = user.IsActive
                ? "User has been unblocked."
                : "User has been blocked.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> PromoteMemberToAdmin(int memberId)
        {
            var member = await _members.GetByIdAsync(memberId);
            if (member == null)
            {
                return NotFound();
            }

            await EnsureAdminRoleAsync();

            var email = member.Email.Trim().ToLowerInvariant();
            var adminUser = await _userMgr.FindByEmailAsync(email);

            if (adminUser == null)
            {
                const string tempPassword = "Admin@12345";
                adminUser = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = member.FullName
                };

                var createRes = await _userMgr.CreateAsync(adminUser, tempPassword);
                if (!createRes.Succeeded)
                {
                    TempData["UserAction"] = $"Failed to promote member: {string.Join(" | ", createRes.Errors.Select(e => e.Description))}";
                    return RedirectToAction(nameof(Index));
                }

                var roleRes = await _userMgr.AddToRoleAsync(adminUser, AdminRole);
                if (!roleRes.Succeeded)
                {
                    TempData["UserAction"] = $"Member account created but role assignment failed: {string.Join(" | ", roleRes.Errors.Select(e => e.Description))}";
                    return RedirectToAction(nameof(Index));
                }

                TempData["UserAction"] = $"Member promoted to admin. Temporary password: {tempPassword}";
                return RedirectToAction(nameof(Index));
            }

            if (!await _userMgr.IsInRoleAsync(adminUser, AdminRole))
            {
                var roleRes = await _userMgr.AddToRoleAsync(adminUser, AdminRole);
                if (!roleRes.Succeeded)
                {
                    TempData["UserAction"] = $"Failed to assign admin role: {string.Join(" | ", roleRes.Errors.Select(e => e.Description))}";
                    return RedirectToAction(nameof(Index));
                }
            }

            if (string.IsNullOrWhiteSpace(adminUser.FullName))
            {
                adminUser.FullName = member.FullName;
                await _userMgr.UpdateAsync(adminUser);
            }

            TempData["UserAction"] = "Member is now an admin.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> DemoteAdminToMember(int memberId)
        {
            var member = await _members.GetByIdAsync(memberId);
            if (member == null)
            {
                return NotFound();
            }

            var email = member.Email.Trim().ToLowerInvariant();
            var adminUser = await _userMgr.FindByEmailAsync(email);
            if (adminUser == null)
            {
                TempData["UserAction"] = "No identity admin account found for this member.";
                return RedirectToAction(nameof(Index));
            }

            if (!await _userMgr.IsInRoleAsync(adminUser, AdminRole))
            {
                TempData["UserAction"] = "This member is already a normal user.";
                return RedirectToAction(nameof(Index));
            }

            var currentAdminEmail = User?.Identity?.Name?.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(currentAdminEmail) && currentAdminEmail == email)
            {
                TempData["UserAction"] = "You cannot remove Admin role from your current account.";
                return RedirectToAction(nameof(Index));
            }

            var admins = await _userMgr.GetUsersInRoleAsync(AdminRole);
            if (admins.Count <= 1)
            {
                TempData["UserAction"] = "Cannot demote the last admin account.";
                return RedirectToAction(nameof(Index));
            }

            var removeRoleRes = await _userMgr.RemoveFromRoleAsync(adminUser, AdminRole);
            if (!removeRoleRes.Succeeded)
            {
                TempData["UserAction"] = $"Failed to demote admin: {string.Join(" | ", removeRoleRes.Errors.Select(e => e.Description))}";
                return RedirectToAction(nameof(Index));
            }

            TempData["UserAction"] = "Admin converted back to normal user.";
            return RedirectToAction(nameof(Index));
        }

        private async System.Threading.Tasks.Task<UsersIndexVm> BuildPageVmAsync()
        {
            var members = await _members.GetAllAsync();
            var adminUsers = await _userMgr.GetUsersInRoleAsync(AdminRole);

            return new UsersIndexVm
            {
                Members = members.OrderByDescending(x => x.CreatedAt).ToList(),
                AdminUsers = adminUsers.OrderByDescending(x => x.CreatedAt).ToList()
            };
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
