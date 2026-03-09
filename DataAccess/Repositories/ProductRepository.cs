using Core.Application.Catalog.Contracts;
using DataAccess.Data;
using Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories;

public class ProductRepository : Repository<Product>, IProductRepository
{
    public ProductRepository(ApplicationDbContext db) : base(db)
    {
    }

    public async Task<List<Product>> GetAllWithCategoryAndImagesAsync()
    {
        return await Db.Products
            .Include(p => p.Category)
            .Include(p => p.Images)
            .ToListAsync();
    }

    public async Task<Product?> GetWithCategoryAndImagesAsync(int id)
    {
        return await Db.Products
            .Include(p => p.Category)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id);
    }
}
