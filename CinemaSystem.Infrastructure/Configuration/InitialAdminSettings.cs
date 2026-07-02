namespace CinemaSystem.Infrastructure.Configuration;

public sealed class InitialAdminSettings
{
    public const string SectionName = "InitialAdmin";

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;
}
