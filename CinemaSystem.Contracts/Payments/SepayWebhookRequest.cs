using System.Text.Json.Serialization;

namespace CinemaSystem.Contracts.Payments;

public sealed class SepayWebhookRequest
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty; // contains TransactionCode

    [JsonPropertyName("transferAmount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("referenceCode")]
    public string? ReferenceCode { get; set; }
}
