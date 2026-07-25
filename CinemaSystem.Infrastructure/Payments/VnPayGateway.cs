using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Infrastructure.Payments;

public sealed class VnPayGateway : IPaymentGateway
{
    // These values are fixed by the VNPAY 2.1.0 payment protocol, not environment data.
    private const string ProtocolVersion = "2.1.0";
    private const string PayCommand = "pay";
    private const string CurrencyCode = "VND";
    private const string SecureHashParameter = "vnp_SecureHash";
    private const string SecureHashTypeParameter = "vnp_SecureHashType";
    private const string WindowsVnTimeZoneId = "SE Asia Standard Time";
    private const string IanaVnTimeZoneId = "Asia/Ho_Chi_Minh";
    private const decimal AmountMultiplier = 100m;

    private readonly VnPaySettings _settings;

    public VnPayGateway(IOptions<VnPaySettings> options)
    {
        _settings = options.Value;
    }

    public string ProviderName => DomainConstants.PaymentProvider.VnPayName;

    public string CreateCheckoutUrl(PaymentGatewayCheckoutRequest request)
    {
        EnsureConfigured();

        if (request.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "VNPAY payment amount must be greater than zero.");
        }

        var createTime = ConvertUtcToVnTime(request.CreatedAtUtc);
        var expireTime = ConvertUtcToVnTime(request.ExpiresAtUtc);
        var amount = decimal.ToInt64(request.Amount * AmountMultiplier);

        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Amount"] = amount.ToString(CultureInfo.InvariantCulture),
            ["vnp_Command"] = PayCommand,
            ["vnp_CreateDate"] = createTime.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = CurrencyCode,
            ["vnp_ExpireDate"] = expireTime.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            ["vnp_IpAddr"] = NormalizeIpAddress(request.ClientIpAddress),
            ["vnp_Locale"] = _settings.Locale,
            ["vnp_OrderInfo"] = $"Cinema booking {request.PaymentId}",
            ["vnp_OrderType"] = _settings.OrderType,
            ["vnp_ReturnUrl"] = _settings.ReturnUrl,
            ["vnp_TmnCode"] = _settings.TmnCode,
            ["vnp_TxnRef"] = request.TransactionCode,
            ["vnp_Version"] = ProtocolVersion
        };

        if (!string.IsNullOrWhiteSpace(_settings.PreferredBankCode))
        {
            parameters["vnp_BankCode"] = _settings.PreferredBankCode.Trim();
        }

        var canonicalQuery = BuildCanonicalQuery(parameters);
        var secureHash = ComputeHmacSha512(_settings.HashSecret, canonicalQuery);
        return $"{_settings.PaymentUrl.TrimEnd('?')}?{canonicalQuery}&{SecureHashParameter}={secureHash}";
    }

    public bool HasValidSignature(IReadOnlyDictionary<string, string> parameters)
    {
        EnsureConfigured();

        if (!parameters.TryGetValue(SecureHashParameter, out var suppliedHash)
            || string.IsNullOrWhiteSpace(suppliedHash))
        {
            return false;
        }

        var signedParameters = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in parameters)
        {
            if (string.IsNullOrWhiteSpace(pair.Value)
                || string.Equals(pair.Key, SecureHashParameter, StringComparison.OrdinalIgnoreCase)
                || string.Equals(pair.Key, SecureHashTypeParameter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            signedParameters[pair.Key] = pair.Value;
        }

        var expectedHash = ComputeHmacSha512(
            _settings.HashSecret,
            BuildCanonicalQuery(signedParameters));
        return FixedTimeEquals(expectedHash, suppliedHash);
    }

    private void EnsureConfigured()
    {
        if (!_settings.Enabled)
        {
            throw new InvalidOperationException("VNPAY payment is disabled.");
        }

        if (string.IsNullOrWhiteSpace(_settings.TmnCode)
            || string.IsNullOrWhiteSpace(_settings.HashSecret)
            || !Uri.TryCreate(_settings.PaymentUrl, UriKind.Absolute, out _)
            || !Uri.TryCreate(_settings.ReturnUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException(
                "VNPAY is enabled but its merchant credentials or URLs are not configured.");
        }
    }

    private static string BuildCanonicalQuery(IEnumerable<KeyValuePair<string, string>> parameters)
    {
        return string.Join(
            "&",
            parameters
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair =>
                    $"{WebUtility.UrlEncode(pair.Key)}={WebUtility.UrlEncode(pair.Value)}"));
    }

    private static string ComputeHmacSha512(string secret, string value)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
    }

    private static bool FixedTimeEquals(string expectedHash, string suppliedHash)
    {
        try
        {
            var expected = Convert.FromHexString(expectedHash);
            var supplied = Convert.FromHexString(suppliedHash.Trim());
            return expected.Length == supplied.Length
                && CryptographicOperations.FixedTimeEquals(expected, supplied);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static DateTime ConvertUtcToVnTime(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(
            utc,
            ResolveVnTimeZone());
    }

    private static TimeZoneInfo ResolveVnTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(IanaVnTimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(WindowsVnTimeZoneId);
        }
    }

    private static string NormalizeIpAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Client IP address is required by VNPAY.",
                nameof(value));
        }

        return value.StartsWith("::ffff:", StringComparison.OrdinalIgnoreCase)
            ? value[7..]
            : value;
    }
}
