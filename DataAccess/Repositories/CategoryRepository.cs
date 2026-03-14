using Core.Application.Catalog.Contracts;
using DataAccess.Data;
using Core.Domain.Entities;

namespace DataAccess.Repositories;

public class CategoryRepository : Repository<Category>, ICategoryRepository
{
    public CategoryRepository(ApplicationDbContext db) : base(db)
    {
    }
 
    public override bool Update(Category entity)
    {
        Set.Update(entity);
        return true;
    }
 
    public override bool Remove(Category entity)
    {
        Set.Remove(entity);
        return true;
    }
}
