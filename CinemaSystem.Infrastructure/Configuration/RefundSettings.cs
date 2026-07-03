namespace CinemaSystem.Infrastructure.Configuration;

public sealed class RefundSettings
{
    public const string DevelopmentFrontendBaseUrl = "http://localhost:5173";
    public const string ClaimRoute = "/refunds/claim";
    public const int DefaultClaimTokenMinutes = 5;
    public const int MinimumClaimTokenMinutes = 1;

    public string FrontendBaseUrl { get; set; } = DevelopmentFrontendBaseUrl;

    public int ClaimTokenMinutes { get; set; } = DefaultClaimTokenMinutes;
}
