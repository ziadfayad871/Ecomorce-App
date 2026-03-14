using Core.Application.Catalog.Contracts;
using Core.Domain.Entities;
using DataAccess.Data;
 
namespace DataAccess.Repositories;
 
public class OrderItemRepository : Repository<OrderItem>, IOrderItemRepository
{
    public OrderItemRepository(ApplicationDbContext db) : base(db)
    {
    }
 
    public override bool Update(OrderItem entity)
    {
        Set.Update(entity);
        return true;
    }
 
    public override bool Remove(OrderItem entity)
    {
        Set.Remove(entity);
        return true;
    }
}
