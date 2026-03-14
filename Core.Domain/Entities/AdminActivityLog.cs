namespace Core.Domain.Entities;

public class AdminActivityLog
{
    public int Id { get; set; }
    public string Section { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string ActorEmail { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
