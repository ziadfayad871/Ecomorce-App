using DataAccess.Models.Entities;
using Task.Contracts;


namespace Task.Contracts
{
    public interface IProductRepository : IRepository<Product>
    {
        Task<List<Product>> GetAllWithCategoryAndImagesAsync();
        Task<Product?> GetWithCategoryAndImagesAsync(int id);
    }
}