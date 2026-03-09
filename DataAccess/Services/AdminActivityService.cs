using System.Security.Claims;
using Core.Application.Common.Activities;
using Microsoft.AspNetCore.Http;

namespace DataAccess.Services;

public class AdminActivityService : IAdminActivityService
{
    private const int MaxItems = 200;
    private static readonly object SyncLock = new();
    private static readonly List<AdminActivityItem> Items = new();
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AdminActivityService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
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

        var item = new AdminActivityItem
        {
            Section = string.IsNullOrWhiteSpace(section) ? "عام" : section.Trim(),
            ActorId = actorId.Trim(),
            ActorEmail = actorEmail.Trim(),
            ActorName = string.IsNullOrWhiteSpace(actorName) ? "مستخدم" : actorName.Trim(),
            Message = message.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        lock (SyncLock)
        {
            Items.Insert(0, item);
            if (Items.Count > MaxItems)
            {
                Items.RemoveRange(MaxItems, Items.Count - MaxItems);
            }
        }
    }

    public IReadOnlyList<AdminActivityItem> GetAll()
    {
        lock (SyncLock)
        {
            return Items.ToList();
        }
    }

    public IReadOnlyList<AdminActivityItem> GetByActor(string actorIdOrEmail)
    {
        var value = actorIdOrEmail?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return GetAll();
        }

        lock (SyncLock)
        {
            return Items
                .Where(x =>
                    string.Equals(x.ActorId, value, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.ActorEmail, value, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.ActorName, value, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }
}
