namespace CinemaSystem.Infrastructure.Configuration;

public sealed class VnPaySettings
{
    public const string SectionName = "VnPaySettings";

    public bool Enabled { get; set; }
    public string TmnCode { get; set; } = string.Empty;
    public string HashSecret { get; set; } = string.Empty;
    public string PaymentUrl { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
    public string? PreferredBankCode { get; set; }
    public string Locale { get; set; } = "vn";
    public string OrderType { get; set; } = "other";
}
