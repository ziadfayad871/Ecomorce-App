using DataAccess.Data;
using DataAccess.Models.Entities;
using Task.Contracts;
using Task.Repositories;


namespace YourApp.Repositories
{
    public class CategoryRepository : Repository<Category>, ICategoryRepository
    {
        public CategoryRepository(ApplicationDbContext db) : base(db) { }
    }
}