namespace CinemaSystem.Infrastructure.Configuration;

public sealed class SepaySettings
{
    public string WebhookSecret { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string BankAccount { get; set; } = string.Empty;
    public decimal? DevelopmentPaymentAmountOverride { get; set; }
}
