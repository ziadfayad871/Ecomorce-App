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
        private readonly IProductRepository _products;

        public CategoriesController(
            ICategoryRepository categories,
            IProductRepository products)
        {
            _categories = categories;
            _products = products;
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _categories.GetByIdAsync(id);
            if (category == null)
            {
                return NotFound();
            }

            var linkedProducts = await _products.FindAsync(p => p.CategoryId == id);
            if (linkedProducts.Count > 0)
            {
                TempData["CategoryAction"] = "Cannot delete category while products are linked to it.";
                return RedirectToAction(nameof(Index));
            }

            _categories.Remove(category);
            await _categories.SaveChangesAsync();
            TempData["CategoryAction"] = "Category deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}
