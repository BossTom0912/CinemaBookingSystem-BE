namespace CinemaSystem.Infrastructure.Configuration;

public sealed class RefundSettings
{
    public const string SectionName = "RefundSettings";
    public const string ClaimRoute = "/refunds/claim";
    public const int MinimumClaimTokenMinutes = 1;

    public string FrontendBaseUrl { get; set; } = string.Empty;

    public int ClaimTokenMinutes { get; set; }
}
