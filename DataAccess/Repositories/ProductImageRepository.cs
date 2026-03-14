using Core.Application.Catalog.Contracts;
using Core.Domain.Entities;
using DataAccess.Data;
 
namespace DataAccess.Repositories;
 
public class ProductImageRepository : Repository<ProductImage>, IProductImageRepository
{
    public ProductImageRepository(ApplicationDbContext db) : base(db)
    {
    }
 
    public override bool Update(ProductImage entity)
    {
        Set.Update(entity);
        return true;
    }
 
    public override bool Remove(ProductImage entity)
    {
        Set.Remove(entity);
        return true;
    }
}
