using Core.Domain.Entities;

namespace Core.Application.Common.Identity;

public interface IMemberAuthService
{
    Task<int> RegisterAsync(string fullName, string email, string password);
    Task<Member?> LoginAsync(string email, string password);
    Task<Member?> FindByEmailAsync(string email, bool activeOnly = false);
    Task<Member> RegisterExternalAsync(string fullName, string email);
    Task<RequestPasswordResetOtpResult> SendPasswordResetOtpAsync(string email, CancellationToken cancellationToken = default);
    Task<ResetPasswordWithOtpResult> ResetPasswordWithOtpAsync(string email, string otpCode, string newPassword, CancellationToken cancellationToken = default);
}
