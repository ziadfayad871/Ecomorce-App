using DataAccess.Models.Entities;
using Task.Contracts;


namespace Task.Contracts
{
    public interface ICategoryRepository : IRepository<Category> { }
}