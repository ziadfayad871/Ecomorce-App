using Core.Application.Catalog.Contracts;
using DataAccess.Data;
using Core.Domain.Entities;

namespace DataAccess.Repositories;

public class CategoryRepository : Repository<Category>, ICategoryRepository
{
    public CategoryRepository(ApplicationDbContext db) : base(db)
    {
    }
}
