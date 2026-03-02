using DataAccess.Models.Entities;
using DataAccess.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Task.Areas.Admin.ViewModels;
using Task.Contracts;


namespace Task.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ProductsController : Controller
    {
        private readonly IProductRepository _products;
        private readonly ICategoryRepository _categories;
        private readonly IRepository<ProductImage> _images;
        private readonly ImageService _img;

        public ProductsController(
            IProductRepository products,
            ICategoryRepository categories,
            IRepository<ProductImage> images,
            ImageService img)
        {
            _products = products;
            _categories = categories;
            _images = images;
            _img = img;
        }

        public async Task<IActionResult> Index()
        {
            var items = await _products.GetAllWithCategoryAndImagesAsync();
            return View(items);
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
                CategoryId = vm.CategoryId
            };

            await _products.AddAsync(p);
            await _products.SaveChangesAsync();

            if (vm.Image != null)
            {
                var path = await _img.SaveProductImageAsync(vm.Image);
                await _images.AddAsync(new ProductImage { ProductId = p.Id, Path = path });
                await _images.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var p = await _products.GetWithCategoryAndImagesAsync(id);
            if (p == null) return NotFound();

            ViewBag.Categories = await _categories.GetAllAsync();
            return View(new ProductFormVm
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price,
                CategoryId = p.CategoryId
            });
        }

        [HttpPost]
        public async Task<IActionResult> Edit(ProductFormVm vm)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Categories = await _categories.GetAllAsync();
                return View(vm);
            }

            var p = await _products.GetByIdAsync(vm.Id);
            if (p == null) return NotFound();

            p.Name = vm.Name.Trim();
            p.Price = vm.Price;
            p.CategoryId = vm.CategoryId;
            _products.Update(p);

            if (vm.Image != null)
            {
                var path = await _img.SaveProductImageAsync(vm.Image);
                await _images.AddAsync(new ProductImage { ProductId = p.Id, Path = path });
            }

            await _products.SaveChangesAsync();
            await _images.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}