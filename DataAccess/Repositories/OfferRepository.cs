using Core.Application.Catalog.Contracts;
using Core.Domain.Entities;
using DataAccess.Data;
 
namespace DataAccess.Repositories;
 
public class OfferRepository : Repository<Offer>, IOfferRepository
{
    public OfferRepository(ApplicationDbContext db) : base(db)
    {
    }
 
    public override bool Update(Offer entity)
    {
        Set.Update(entity);
        return true;
    }
 
    public override bool Remove(Offer entity)
    {
        Set.Remove(entity);
        return true;
    }
}
