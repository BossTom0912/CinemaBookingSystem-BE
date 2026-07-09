namespace CinemaSystem.Infrastructure.Configuration;

public sealed class EmailSettings
{
    public const string SectionName = "EmailSettings";

    public string SmtpHost { get; set; } = "smtp.gmail.com";

    public int SmtpPort { get; set; } = 587;

    public string SenderEmail { get; set; } = string.Empty;

    public string SenderName { get; set; } = "Cinema Booking System";

    public string Password { get; set; } = string.Empty;

    public bool UseMock { get; set; }

    public bool AutoConfirmEmail { get; set; }
}
