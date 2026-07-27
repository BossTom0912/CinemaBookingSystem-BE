namespace CinemaSystem.Infrastructure.Configuration;

public sealed class EmailSettings
{
    public const string SectionName = "EmailSettings";

    public const string SmtpProvider = "Smtp";

    public const string ResendProvider = "Resend";

    public string Provider { get; set; } = SmtpProvider;

    public string SmtpHost { get; set; } = "smtp.gmail.com";

    public int SmtpPort { get; set; } = 587;

    public int SendTimeoutSeconds { get; set; } = 15;

    public string SenderEmail { get; set; } = string.Empty;

    public string SenderName { get; set; } = "Cinema Booking System";

    public string Password { get; set; } = string.Empty;

    public string ResendApiKey { get; set; } = string.Empty;

    public string ResendApiBaseUrl { get; set; } = "https://api.resend.com/";

    public bool UseMock { get; set; }

    public bool AutoConfirmEmail { get; set; }

    public bool UsesResend =>
        string.Equals(Provider, ResendProvider, StringComparison.OrdinalIgnoreCase);

    public bool UsesSmtp =>
        string.Equals(Provider, SmtpProvider, StringComparison.OrdinalIgnoreCase);
}
