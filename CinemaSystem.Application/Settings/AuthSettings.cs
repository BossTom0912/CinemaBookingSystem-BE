namespace CinemaSystem.Application.Settings;

public class AuthSettings
{
    public int OtpExpirySeconds { get; set; } = 120;
    public int OtpResendCooldownSeconds { get; set; } = 60;
    public int OtpMaxSendAttempts { get; set; } = 5;
    public int OtpSendLockHours { get; set; } = 2;
    public int InvitationTokenExpiryMinutes { get; set; } = 60;
}
