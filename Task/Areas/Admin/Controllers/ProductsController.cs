using Core.Application.Catalog.Contracts;
using Core.Application.Common.Activities;
using Core.Application.Common.Files;
using Core.Application.Common.Persistence;
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
    public class ProductsController : Controller
    {
        private readonly IProductRepository _products;
        private readonly ICategoryRepository _categories;
        private readonly IProductImageRepository _images;
        private readonly IImageService _img;
        private readonly IAdminActivityService _activity;

        public ProductsController(
            IProductRepository products,
            ICategoryRepository categories,
            IProductImageRepository images,
            IImageService img,
            IAdminActivityService activity)
        {
            _products = products;
            _categories = categories;
            _images = images;
            _img = img;
            _activity = activity;
        }

        public async Task<IActionResult> Index(string? searchTerm = null, int? categoryId = null, int page = 1, int pageSize = 25)
        {
            var allowedPageSizes = new[] { 25, 50, 75, 100 };
            if (!allowedPageSizes.Contains(pageSize))
            {
                pageSize = 25;
            }

            if (page < 1)
            {
                page = 1;
            }

            var allItems = await _products.GetAllWithCategoryAndImagesAsync();
            var query = ApplyFilters(allItems, searchTerm, categoryId);

            var totalItems = query.Count();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));

            if (page > totalPages)
            {
                page = totalPages;
            }

            var items = query
                .OrderByDescending(p => p.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.Categories = await _categories.GetAllAsync();
            ViewBag.SearchTerm = searchTerm?.Trim() ?? string.Empty;
            ViewBag.SelectedCategoryId = categoryId;
            ViewBag.PageSize = pageSize;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;

            return View(items);
        }

        [HttpGet]
        public async Task<IActionResult> Print(string? searchTerm = null, int? categoryId = null)
        {
            var allItems = await _products.GetAllWithCategoryAndImagesAsync();
            var items = ApplyFilters(allItems, searchTerm, categoryId)
                .OrderByDescending(p => p.Id)
                .ToList();

            var categories = await _categories.GetAllAsync();
            var selectedCategoryName = categoryId.HasValue
                ? categories.FirstOrDefault(c => c.Id == categoryId.Value)?.Name ?? "كل الأقسام"
                : "كل الأقسام";

            ViewBag.SearchTerm = searchTerm?.Trim() ?? string.Empty;
            ViewBag.SelectedCategoryName = selectedCategoryName;

            return View(items);
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(string? searchTerm = null, int? categoryId = null)
        {
            var allItems = await _products.GetAllWithCategoryAndImagesAsync();
            var items = ApplyFilters(allItems, searchTerm, categoryId)
                .OrderByDescending(p => p.Id)
                .ToList();

            var csv = new StringBuilder();
            csv.AppendLine("اسم المنتج,القسم,السعر,المخزون,تاريخ الإنشاء");

            foreach (var item in items)
            {
                var name = EscapeCsv(item.Name);
                var category = EscapeCsv(item.Category?.Name ?? "بدون قسم");
                var price = item.Price.ToString("0.00", CultureInfo.InvariantCulture);
                var stock = item.StockQuantity.ToString(CultureInfo.InvariantCulture);
                var createdAt = item.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                csv.AppendLine($"{name},{category},{price},{stock},{createdAt}");
            }

            var data = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            var fileName = $"products-{DateTime.Now:yyyyMMdd-HHmmss}.csv";

            _activity.Add("المنتجات", "تم تصدير بيانات المنتجات.");

            return File(data, "text/csv; charset=utf-8", fileName);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            ViewBag.Categories = await _categories.GetAllAsync();
            return View(new ProductFormVm());
        }

        [HttpPost]
        public async Task<IActionResult> Create(ProductFormVm vm)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Categories = await _categories.GetAllAsync();
                return View(vm);
            }

            var p = new Product
            {
                Name = vm.Name.Trim(),
                Price = vm.Price,
                StockQuantity = vm.StockQuantity,
                CategoryId = vm.CategoryId
            };

            await _products.AddAsync(p);
            await _products.SaveChangesAsync();

            if (vm.Image != null)
            {
                await using var imageStream = vm.Image.OpenReadStream();
                var path = await _img.SaveProductImageAsync(imageStream, vm.Image.FileName);
                await _images.AddAsync(new ProductImage { ProductId = p.Id, Path = path });
                await _images.SaveChangesAsync();
            }

            var createdMsg = "تم إضافة المنتج بنجاح.";
            TempData["ProductAction"] = createdMsg;
            _activity.Add("المنتجات", createdMsg);
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var p = await _products.GetWithCategoryAndImagesAsync(id);
            if (p == null)
            {
                return NotFound();
            }

            ViewBag.Categories = await _categories.GetAllAsync();
            return View(new ProductFormVm
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                CategoryId = p.CategoryId,
                ExistingImagePath = p.Images?.FirstOrDefault()?.Path
            });
        }

        [HttpPost]
        public async Task<IActionResult> Edit(ProductFormVm vm)
        {
            if (!ModelState.IsValid)
            {
                var currentProduct = await _products.GetWithCategoryAndImagesAsync(vm.Id);
                vm.ExistingImagePath = currentProduct?.Images?.FirstOrDefault()?.Path;
                ViewBag.Categories = await _categories.GetAllAsync();
                return View(vm);
            }

            var p = await _products.GetByIdAsync(vm.Id);
            if (p == null)
            {
                return NotFound();
            }

            p.Name = vm.Name.Trim();
            p.Price = vm.Price;
            p.StockQuantity = vm.StockQuantity;
            p.CategoryId = vm.CategoryId;
            _products.Update(p);

            if (vm.Image != null)
            {
                await using var imageStream = vm.Image.OpenReadStream();
                var path = await _img.SaveProductImageAsync(imageStream, vm.Image.FileName);
                await _images.AddAsync(new ProductImage { ProductId = p.Id, Path = path });
            }

            await _products.SaveChangesAsync();
            await _images.SaveChangesAsync();

            var updatedMsg = "تم تحديث المنتج بنجاح.";
            TempData["ProductAction"] = updatedMsg;
            _activity.Add("المنتجات", updatedMsg);

            return RedirectToAction(nameof(Edit), new { id = p.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _products.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            var images = await _images.FindAsync(x => x.ProductId == id);
            foreach (var image in images)
            {
                _images.Remove(image);
            }

            _products.Remove(product);
            await _images.SaveChangesAsync();
            await _products.SaveChangesAsync();

            var deletedMsg = "تم حذف المنتج بنجاح.";
            TempData["ProductAction"] = deletedMsg;
            _activity.Add("المنتجات", deletedMsg);
            return RedirectToAction(nameof(Index));
        }

        private static IEnumerable<Product> ApplyFilters(IEnumerable<Product> products, string? searchTerm, int? categoryId)
        {
            var query = products;

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var normalizedSearch = searchTerm.Trim();
                query = query.Where(p => p.Name.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase));
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            return query;
        }

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "\"\"";
            }

            var escaped = value.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }
    }
}
