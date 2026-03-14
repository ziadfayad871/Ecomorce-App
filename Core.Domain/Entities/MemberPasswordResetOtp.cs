using System.ComponentModel.DataAnnotations;

namespace Core.Domain.Entities;

public class MemberPasswordResetOtp
{
    public int Id { get; set; }

    public int MemberId { get; set; }
    public Member? Member { get; set; }

    [Required]
    public string CodeHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public int FailedAttempts { get; set; }
}
