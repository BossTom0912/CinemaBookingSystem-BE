using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using CinemaSystem.Infrastructure.Configuration;

namespace CinemaSystem.Infrastructure.Services;

public sealed class HmacVerifyHelper
{
    private readonly SepaySettings _settings;

    public HmacVerifyHelper(IOptions<SepaySettings> options)
    {
        _settings = options.Value;
    }

    // Verify signature using timestamp.payload and secret key (sha256)
    public bool Verify(string signature, string timestamp, string payload)
    {
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(timestamp)) return false;

        var secret = _settings.WebhookSecret ?? string.Empty;
        var expectedRaw = timestamp + "." + payload;
        var expectedHash = ComputeHmacSha256(expectedRaw, secret);
        var expected = "sha256=" + expectedHash;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature));
    }

    private static string ComputeHmacSha256(string data, string key)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key ?? string.Empty);
        using var hmac = new HMACSHA256(keyBytes);
        var bytes = Encoding.UTF8.GetBytes(data);
        var hash = hmac.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }
}
