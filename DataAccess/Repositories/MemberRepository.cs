using Core.Application.Members.Contracts;
using Core.Domain.Entities;
using DataAccess.Data;
 
namespace DataAccess.Repositories;
 
public class MemberRepository : Repository<Member>, IMemberRepository
{
    public MemberRepository(ApplicationDbContext db) : base(db)
    {
    }
 
    public override bool Update(Member entity)
    {
        Set.Update(entity);
        return true;
    }
 
    public override bool Remove(Member entity)
    {
        Set.Remove(entity);
        return true;
    }
}
