using Core.Application.Common.Persistence;
using Core.Domain.Entities;
 
namespace Core.Application.Catalog.Contracts;
 
public interface IOrderRepository : IRepository<Order>
{
}
