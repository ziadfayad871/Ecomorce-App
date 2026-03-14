namespace DataAccess.Options;

public class PasswordResetOtpOptions
{
    public int CodeLength { get; set; } = 6;
    public int ExpiryMinutes { get; set; } = 10;
    public int MaxFailedAttempts { get; set; } = 5;
}
