using DataAccess.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Task.Areas.Admin.ViewModels;
using Task.Contracts;


namespace Task.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class CategoriesController : Controller
    {
        private readonly ICategoryRepository _categories;

        public CategoriesController(ICategoryRepository categories)
        {
            _categories = categories;
        }

        public async Task<IActionResult> Index()
        {
            var items = await _categories.GetAllAsync();
            return View(items);
        }

        [HttpGet]
        public IActionResult Create() => View(new CategoryFormVm());

        [HttpPost]
        public async Task<IActionResult> Create(CategoryFormVm vm)
        {
            if (!ModelState.IsValid) return View(vm);

            await _categories.AddAsync(new Category { Name = vm.Name.Trim() });
            await _categories.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var c = await _categories.GetByIdAsync(id);
            if (c == null) return NotFound();

            return View(new CategoryFormVm { Id = c.Id, Name = c.Name });
        }

        [HttpPost]
        public async Task<IActionResult> Edit(CategoryFormVm vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var c = await _categories.GetByIdAsync(vm.Id);
            if (c == null) return NotFound();

            c.Name = vm.Name.Trim();
            _categories.Update(c);
            await _categories.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}