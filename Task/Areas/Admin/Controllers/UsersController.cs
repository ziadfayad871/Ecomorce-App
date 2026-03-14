using Core.Application.Common.Activities;
using Core.Application.Common.Persistence;
using Core.Application.Members.Contracts;
using DataAccess.Models.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Task.Areas.Admin.ViewModels;
using MemberEntity = Core.Domain.Entities.Member;

namespace Task.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private const string AdminRole = "Admin";
        private readonly IMemberRepository _members;
        private readonly UserManager<ApplicationUser> _userMgr;
        private readonly RoleManager<IdentityRole> _roleMgr;
        private readonly IAdminActivityService _activity;

        public UsersController(
            IMemberRepository members,
            UserManager<ApplicationUser> userMgr,
            RoleManager<IdentityRole> roleMgr,
            IAdminActivityService activity)
        {
            _members = members;
            _userMgr = userMgr;
            _roleMgr = roleMgr;
            _activity = activity;
        }

        [HttpGet]
        public IActionResult Index() => View();

        [HttpGet]
        public async System.Threading.Tasks.Task<IActionResult> Admins(string? searchTerm = null)
        {
            var admins = await _userMgr.GetUsersInRoleAsync(AdminRole);
            var query = admins.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var normalizedSearch = searchTerm.Trim();
                query = query.Where(x =>
                    (!string.IsNullOrWhiteSpace(x.FullName) && x.FullName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(x.Email) && x.Email.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
                );
            }

            ViewBag.SearchTerm = searchTerm?.Trim() ?? string.Empty;
            return View(query.OrderByDescending(x => x.Id).ToList());
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

                var existingMsg = "البريد موجود بالفعل وتم منحه صلاحية مشرف.";
                TempData["UserAction"] = existingMsg;
                _activity.Add("المستخدمين", existingMsg);
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
                var failedRoleMsg = $"تم إنشاء الحساب لكن فشل تعيين صلاحية المشرف: {string.Join(" | ", roleRes.Errors.Select(e => e.Description))}";
                TempData["UserError"] = failedRoleMsg;
                _activity.Add("المستخدمين", failedRoleMsg);
                ModelState.Clear();
                return View(new CreateAdminVm());
            }

            var createdMsg = "تم إنشاء حساب مشرف جديد بنجاح.";
            TempData["UserAction"] = createdMsg;
            _activity.Add("المستخدمين", createdMsg);
            return RedirectToAction(nameof(Admins));
        }

        [HttpGet]
        public async System.Threading.Tasks.Task<IActionResult> Members(string? searchTerm = null)
        {
            var admins = await _userMgr.GetUsersInRoleAsync(AdminRole);
            var adminEmails = admins
                .Select(x => x.Email?.Trim().ToLowerInvariant() ?? string.Empty)
                .ToHashSet();

            var allMembers = await _members.GetAllAsync();
            var regularMembers = allMembers
                .Where(x => !adminEmails.Contains(x.Email.Trim().ToLowerInvariant()))
                .ToList();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var normalizedSearch = searchTerm.Trim();
                regularMembers = regularMembers
                    .Where(x =>
                        (!string.IsNullOrWhiteSpace(x.FullName) && x.FullName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(x.Email) && x.Email.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            ViewBag.SearchTerm = searchTerm?.Trim() ?? string.Empty;
            return View(regularMembers.OrderByDescending(x => x.Id).ToList());
        }

        [HttpGet]
        public async System.Threading.Tasks.Task<IActionResult> EditMember(int id)
        {
            var member = await _members.GetByIdAsync(id);
            if (member == null)
            {
                return NotFound();
            }

            var availableRoles = await _roleMgr.Roles
                .Where(r => r.Name != "Member")
                .Select(r => r.Name!)
                .ToListAsync();

            var appUser = await _userMgr.FindByEmailAsync(member.Email);
            string? selectedRole = null;
            if (appUser != null)
            {
                var userRoles = await _userMgr.GetRolesAsync(appUser);
                selectedRole = userRoles.FirstOrDefault(r => r != "Member");
            }

            return View(new MemberEditVm
            {
                Id = member.Id,
                FullName = member.FullName,
                Email = member.Email,
                IsActive = member.IsActive,
                SelectedRole = selectedRole,
                AvailableRoles = availableRoles
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> EditMember(MemberEditVm vm)
        {
            var availableRoles = await _roleMgr.Roles
                .Where(r => r.Name != "Member")
                .Select(r => r.Name!)
                .ToListAsync();
            vm.AvailableRoles = availableRoles;

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var member = await _members.GetByIdAsync(vm.Id);
            if (member == null)
            {
                return NotFound();
            }

            var normalizedEmail = vm.Email.Trim().ToLowerInvariant();
            var otherMembers = await _members.GetAllAsync();
            var emailExistsForAnotherMember = otherMembers.Any(x =>
                x.Id != vm.Id &&
                string.Equals(x.Email?.Trim(), normalizedEmail, StringComparison.OrdinalIgnoreCase));

            if (emailExistsForAnotherMember)
            {
                ModelState.AddModelError(nameof(vm.Email), "هذا البريد مستخدم بالفعل.");
                return View(vm);
            }

            var adminUser = await _userMgr.FindByEmailAsync(normalizedEmail);

            member.FullName = vm.FullName.Trim();
            member.Email = normalizedEmail;
            member.IsActive = vm.IsActive;

            _members.Update(member);
            await _members.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(vm.SelectedRole))
            {
                if (adminUser == null)
                {
                    adminUser = new ApplicationUser
                    {
                        UserName = normalizedEmail,
                        Email = normalizedEmail,
                        EmailConfirmed = true,
                        FullName = member.FullName
                    };
                    await _userMgr.CreateAsync(adminUser, "Temp-Pw!234");
                }

                var currentRoles = await _userMgr.GetRolesAsync(adminUser);
                await _userMgr.RemoveFromRolesAsync(adminUser, currentRoles);
                await _userMgr.AddToRoleAsync(adminUser, vm.SelectedRole);
            }
            else if (adminUser != null)
            {
                var currentRoles = await _userMgr.GetRolesAsync(adminUser);
                await _userMgr.RemoveFromRolesAsync(adminUser, currentRoles);
            }

            var updatedMsg = "تم تحديث بيانات العضو والصلاحيات بنجاح.";
            TempData["UserAction"] = updatedMsg;
            _activity.Add("المستخدمين", updatedMsg);

            return RedirectToAction(nameof(EditMember), new { id = member.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> DeleteMember(int id)
        {
            var member = await _members.GetByIdAsync(id);
            if (member == null)
            {
                return NotFound();
            }

            _members.Remove(member);
            await _members.SaveChangesAsync();

            var deletedMsg = "تم حذف العضو بنجاح.";
            TempData["UserAction"] = deletedMsg;
            _activity.Add("المستخدمين", deletedMsg);

            return RedirectToAction(nameof(Members));
        }

        [HttpGet]
        public async System.Threading.Tasks.Task<IActionResult> Roles()
        {
            var admins = await _userMgr.GetUsersInRoleAsync(AdminRole);
            var adminEmails = admins
                .Select(x => x.Email?.Trim().ToLowerInvariant() ?? string.Empty)
                .ToHashSet();

            var allMembers = await _members.GetAllAsync();
            var membersCount = allMembers.Count(x => !adminEmails.Contains((x.Email ?? string.Empty).Trim().ToLowerInvariant()));

            var vm = new List<RoleSummaryVm>
            {
                new RoleSummaryVm
                {
                    Name = AdminRole,
                    DisplayName = "المشرفين",
                    UsersCount = admins.Count,
                    BrowseAction = nameof(Admins)
                },
                new RoleSummaryVm
                {
                    Name = "Member",
                    DisplayName = "الأعضاء",
                    UsersCount = membersCount,
                    BrowseAction = nameof(Members)
                }
            };

            var otherRoles = await _roleMgr.Roles
                .OrderBy(r => r.Name)
                .ToListAsync();

            foreach (var role in otherRoles)
            {
                var roleName = role.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(roleName) ||
                    string.Equals(roleName, AdminRole, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(roleName, "Member", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var users = await _userMgr.GetUsersInRoleAsync(roleName);
                vm.Add(new RoleSummaryVm
                {
                    Name = roleName,
                    DisplayName = roleName,
                    UsersCount = users.Count
                });
            }

            return View(vm);
        }

        [HttpGet]
        public async System.Threading.Tasks.Task<IActionResult> Logs(string? actor = null)
        {
            IReadOnlyList<AdminActivityItem> logs;
            string? actorDisplayName = null;

            if (string.IsNullOrWhiteSpace(actor))
            {
                logs = _activity.GetAll();
            }
            else
            {
                logs = _activity.GetByActor(actor);
                var actorUser = await _userMgr.FindByIdAsync(actor) ?? await _userMgr.FindByEmailAsync(actor);
                actorDisplayName = actorUser?.FullName ?? actorUser?.Email ?? actor;
            }

            ViewBag.ActorFilter = actor;
            ViewBag.ActorDisplayName = actorDisplayName;
            return View(logs);
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
                var blockedAdminMsg = "غير مسموح بحظر حسابات المشرفين.";
                TempData["UserError"] = blockedAdminMsg;
                _activity.Add("المستخدمين", blockedAdminMsg);
                return RedirectToAction(nameof(Members));
            }

            user.IsActive = !user.IsActive;
            _members.Update(user);
            await _members.SaveChangesAsync();

            var statusMsg = user.IsActive
                ? "تم فك حظر العضو بنجاح."
                : "تم حظر العضو بنجاح.";
            TempData["UserAction"] = statusMsg;
            _activity.Add("المستخدمين", statusMsg);

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
