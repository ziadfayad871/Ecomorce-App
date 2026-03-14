namespace Core.Application.Common.Identity;

public record RequestPasswordResetOtpResult(bool Success, string Message);

public record ResetPasswordWithOtpResult(bool Success, string Message);
