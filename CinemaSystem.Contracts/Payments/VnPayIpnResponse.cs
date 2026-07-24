using System.Text.Json.Serialization;

namespace CinemaSystem.Contracts.Payments;

public sealed class VnPayIpnResponse
{
    [JsonPropertyName("RspCode")]
    public string ResponseCode { get; init; } = string.Empty;

    [JsonPropertyName("Message")]
    public string Message { get; init; } = string.Empty;
}
