namespace CinemaSystem.Application.Settings;

public class AuthSettings
{
    public const string SectionName = "AuthSettings";

    public int OtpExpirySeconds { get; set; } = 120;
    public int OtpResendCooldownSeconds { get; set; } = 60;
    public int OtpMaxSendAttempts { get; set; } = 5;
    public int OtpSendLockHours { get; set; } = 2;
    public int InvitationTokenExpiryMinutes { get; set; } = 60;
    
    public int PasswordMinLength { get; set; } = 8;
    public int PasswordMaxLength { get; set; } = 100;
    public bool PasswordRequireUppercase { get; set; } = true;
    public bool PasswordRequireLowercase { get; set; } = true;
    public bool PasswordRequireDigit { get; set; } = true;
}
