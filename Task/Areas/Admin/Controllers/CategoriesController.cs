using Core.Application.Catalog.Contracts;
using Core.Application.Common.Activities;
using Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;
using Task.Areas.Admin.ViewModels;

namespace Task.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class CategoriesController : Controller
    {
        private readonly ICategoryRepository _categories;
        private readonly IProductRepository _products;
        private readonly IAdminActivityService _activity;

        public CategoriesController(
            ICategoryRepository categories,
            IProductRepository products,
            IAdminActivityService activity)
        {
            _categories = categories;
            _products = products;
            _activity = activity;
        }

        public async Task<IActionResult> Index()
        {
            var items = await _categories.GetAllAsync();
            var allProducts = await _products.GetAllAsync();

            ViewBag.ProductCounts = allProducts
                .GroupBy(x => x.CategoryId)
                .ToDictionary(x => x.Key, x => x.Count());

            return View(items.OrderByDescending(x => x.Id).ToList());
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel()
        {
            var items = (await _categories.GetAllAsync())
                .OrderBy(x => x.Name)
                .ToList();
            var allProducts = await _products.GetAllAsync();
            var productCounts = allProducts
                .GroupBy(x => x.CategoryId)
                .ToDictionary(x => x.Key, x => x.Count());

            var csv = new StringBuilder();
            csv.AppendLine("اسم القسم,عدد المنتجات");

            foreach (var item in items)
            {
                var name = EscapeCsv(item.Name);
                var count = productCounts.TryGetValue(item.Id, out var total) ? total : 0;
                csv.AppendLine($"{name},{count.ToString(CultureInfo.InvariantCulture)}");
            }

            var data = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            var fileName = $"categories-{DateTime.Now:yyyyMMdd-HHmmss}.csv";

            _activity.Add("الأقسام", "تم تصدير بيانات الأقسام.");

            return File(data, "text/csv; charset=utf-8", fileName);
        }

        [HttpGet]
        public async Task<IActionResult> SectionProducts(int id)
        {
            var section = await _categories.GetByIdAsync(id);
            if (section == null)
            {
                return NotFound();
            }

            var allProducts = await _products.GetAllWithCategoryAndImagesAsync();
            var sectionProducts = allProducts
                .Where(p => p.CategoryId == id)
                .OrderByDescending(p => p.Id)
                .ToList();

            var vm = new CategoryProductsVm
            {
                CategoryId = section.Id,
                CategoryName = section.Name,
                Products = sectionProducts
            };

            return View(vm);
        }

        [HttpGet]
        public IActionResult Create() => View(new CategoryFormVm());

        [HttpPost]
        public async Task<IActionResult> Create(CategoryFormVm vm)
        {
            if (!ModelState.IsValid) return View(vm);

            await _categories.AddAsync(new Category { Name = vm.Name.Trim() });
            await _categories.SaveChangesAsync();

            var createdMsg = "تم إضافة القسم بنجاح.";
            TempData["CategoryAction"] = createdMsg;
            _activity.Add("الأقسام", createdMsg);
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

            var updatedMsg = "تم تحديث القسم بنجاح.";
            TempData["CategoryAction"] = updatedMsg;
            _activity.Add("الأقسام", updatedMsg);

            return RedirectToAction(nameof(Edit), new { id = c.Id });
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
                var blockedMsg = "لا يمكن حذف القسم لوجود منتجات مرتبطة به.";
                TempData["CategoryAction"] = blockedMsg;
                _activity.Add("الأقسام", blockedMsg);
                return RedirectToAction(nameof(Index));
            }

            _categories.Remove(category);
            await _categories.SaveChangesAsync();

            var deletedMsg = "تم حذف القسم بنجاح.";
            TempData["CategoryAction"] = deletedMsg;
            _activity.Add("الأقسام", deletedMsg);
            return RedirectToAction(nameof(Index));
        }

        private static string EscapeCsv(string? value)
        {
            var text = value?.Trim() ?? string.Empty;
            return $"\"{text.Replace("\"", "\"\"")}\"";
        }
    }
}
