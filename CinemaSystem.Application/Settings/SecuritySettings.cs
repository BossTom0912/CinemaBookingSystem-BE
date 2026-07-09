namespace CinemaSystem.Application.Settings;

public class SecuritySettings
{
    public const string SectionName = "SecuritySettings";

    public string ConfirmationTokenSecret { get; set; } = string.Empty;
}
