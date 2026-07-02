namespace CinemaSystem.Configuration;

public sealed class CorsSettings
{
    public const string SectionName = "CorsSettings";

    public string[] AllowedOrigins { get; set; } = [];
}
