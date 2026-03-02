using DataAccess.Data;
using DataAccess.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Task.Contracts;
using Task.Repositories;


namespace Task.Repositories
{
    public class ProductRepository : Repository<Product>, IProductRepository
    {
        public ProductRepository(ApplicationDbContext db) : base(db) { }

        public async Task<List<Product>> GetAllWithCategoryAndImagesAsync()
        {
            return await _db.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                .ToListAsync();
        }

        public async Task<Product?> GetWithCategoryAndImagesAsync(int id)
        {
            return await _db.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);
        }
    }
}