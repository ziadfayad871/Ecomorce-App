using Core.Application.Common.Persistence;
using Core.Domain.Entities;

namespace Core.Application.Catalog.Contracts;

public interface IProductRepository : IRepository<Product>
{
    Task<List<Product>> GetAllWithCategoryAndImagesAsync();
    Task<Product?> GetWithCategoryAndImagesAsync(int id);
}
