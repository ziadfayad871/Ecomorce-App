using System.Security.Claims;
using Core.Application.Common.Activities;
using DataAccess.Data;
using Core.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
 
namespace DataAccess.Services;
 
public class AdminActivityService : IAdminActivityService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAdminActivityLogRepository _logRepo;
 
    public AdminActivityService(IHttpContextAccessor httpContextAccessor, IAdminActivityLogRepository logRepo)
    {
        _httpContextAccessor = httpContextAccessor;
        _logRepo = logRepo;
    }
 
    public void Add(string section, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }
 
        var user = _httpContextAccessor.HttpContext?.User;
        var actorId = user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var actorEmail = user?.FindFirstValue(ClaimTypes.Email)
            ?? user?.Identity?.Name
            ?? string.Empty;
        var actorName = user?.Identity?.Name;
 
        var log = new AdminActivityLog
        {
            Section = string.IsNullOrWhiteSpace(section) ? "عام" : section.Trim(),
            ActorId = actorId.Trim(),
            ActorEmail = actorEmail.Trim(),
            ActorName = string.IsNullOrWhiteSpace(actorName) ? "مستخدم" : actorName.Trim(),
            Message = message.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
 
        _logRepo.AddAsync(log).AsTask().GetAwaiter().GetResult();
        _logRepo.SaveChangesAsync().GetAwaiter().GetResult();
    }
 
    public IReadOnlyList<AdminActivityItem> GetAll()
    {
        return _logRepo.GetAllAsync().GetAwaiter().GetResult()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new AdminActivityItem
            {
                Section = x.Section,
                ActorId = x.ActorId,
                ActorEmail = x.ActorEmail,
                ActorName = x.ActorName,
                Message = x.Message,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToList();
    }
 
    public IReadOnlyList<AdminActivityItem> GetByActor(string actorIdOrEmail)
    {
        var value = actorIdOrEmail?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return GetAll();
        }
 
        return _logRepo.FindAsync(x =>
                x.ActorId == value ||
                x.ActorEmail == value ||
                x.ActorName == value).GetAwaiter().GetResult()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new AdminActivityItem
            {
                Section = x.Section,
                ActorId = x.ActorId,
                ActorEmail = x.ActorEmail,
                ActorName = x.ActorName,
                Message = x.Message,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToList();
    }
}
