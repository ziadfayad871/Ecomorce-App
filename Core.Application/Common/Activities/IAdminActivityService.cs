namespace Core.Application.Common.Activities;

public interface IAdminActivityService
{
    void Add(string section, string message);
    IReadOnlyList<AdminActivityItem> GetAll();
    IReadOnlyList<AdminActivityItem> GetByActor(string actorIdOrEmail);
}
