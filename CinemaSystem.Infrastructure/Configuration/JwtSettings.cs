namespace CinemaSystem.Infrastructure.Configuration;

public sealed class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string Issuer { get; set; } = "CinemaSystem";

    public string Audience { get; set; } = "CinemaSystem.Api";

    public string Secret { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 7;

    public int ClockSkewSeconds { get; set; } = 60;
}
