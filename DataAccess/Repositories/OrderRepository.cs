using Core.Application.Catalog.Contracts;
using Core.Domain.Entities;
using DataAccess.Data;
 
namespace DataAccess.Repositories;
 
public class OrderRepository : Repository<Order>, IOrderRepository
{
    public OrderRepository(ApplicationDbContext db) : base(db)
    {
    }
 
    public override bool Update(Order entity)
    {
        Set.Update(entity);
        return true;
    }
 
    public override bool Remove(Order entity)
    {
        Set.Remove(entity);
        return true;
    }
}
