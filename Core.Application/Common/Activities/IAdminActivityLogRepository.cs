using Core.Application.Common.Persistence;
using Core.Domain.Entities;
 
namespace Core.Application.Common.Activities;
 
public interface IAdminActivityLogRepository : IRepository<AdminActivityLog>
{
}
