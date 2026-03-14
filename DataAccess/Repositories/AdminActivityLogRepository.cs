using Core.Application.Common.Activities;
using Core.Domain.Entities;
using DataAccess.Data;
 
namespace DataAccess.Repositories;
 
public class AdminActivityLogRepository : Repository<AdminActivityLog>, IAdminActivityLogRepository
{
    public AdminActivityLogRepository(ApplicationDbContext db) : base(db)
    {
    }
 
    public override bool Update(AdminActivityLog entity)
    {
        Set.Update(entity);
        return true;
    }
 
    public override bool Remove(AdminActivityLog entity)
    {
        Set.Remove(entity);
        return true;
    }
}
