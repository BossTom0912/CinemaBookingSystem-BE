namespace CinemaSystem.Infrastructure.Configuration;

public sealed class RefundSettings
{
    public const string SectionName = "RefundSettings";
    public const string ClaimRoute = "/refunds/claim";
    public const string CustomerConfirmationRoute = "/refunds/confirm";
    public const int MinimumClaimTokenMinutes = 1;
    public const int DefaultCustomerConfirmationHours = 24;

    public string FrontendBaseUrl { get; set; } = string.Empty;

    public int ClaimTokenMinutes { get; set; }

    public int CustomerConfirmationHours { get; set; } = DefaultCustomerConfirmationHours;
}
