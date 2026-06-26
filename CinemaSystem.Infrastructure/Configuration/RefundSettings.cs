namespace CinemaSystem.Infrastructure.Configuration;

public sealed class RefundSettings
{
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";
    public int ClaimTokenMinutes { get; set; } = 5;
}
