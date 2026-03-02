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
    public class DashboardController : Controller
    {
        private const string AdminRole = "Admin";
        private readonly ICategoryRepository _categories;
        private readonly IProductRepository _products;
        private readonly IRepository<MemberEntity> _members;
        private readonly UserManager<ApplicationUser> _userMgr;

        public DashboardController(
            ICategoryRepository categories,
            IProductRepository products,
            IRepository<MemberEntity> members,
            UserManager<ApplicationUser> userMgr)
        {
            _categories = categories;
            _products = products;
            _members = members;
            _userMgr = userMgr;
        }

        [HttpGet]
        public async System.Threading.Tasks.Task<IActionResult> Index()
        {
            var categories = await _categories.GetAllAsync();
            var products = await _products.GetAllAsync();
            var members = await _members.GetAllAsync();
            var admins = await _userMgr.GetUsersInRoleAsync(AdminRole);

            var model = new DashboardVm
            {
                DesignsCount = categories.Count,
                ProductsCount = products.Count,
                MembersCount = members.Count,
                ActiveMembersCount = members.Count(x => x.IsActive),
                AdminsCount = admins.Count
            };

            return View(model);
        }
    }
}
