using System.Security.Cryptography;
using System.Text;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Infrastructure.Security;

public sealed class RefundClaimIssuer : IRefundClaimIssuer
{
    private readonly RefundSettings _settings;

    public RefundClaimIssuer(IOptions<RefundSettings> options)
    {
        _settings = options.Value;
    }

    public RefundClaimIssue Create(string refundId, string customerProfileId, DateTime now)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var claim = new RefundClaim
        {
            RefundClaimId = NewId("RFC"),
            RefundId = refundId,
            CustomerProfileId = customerProfileId,
            ClaimStatus = BookingConstants.RefundClaimStatus.PendingInfo,
            AccountValidationStatus = BookingConstants.AccountValidationStatus.NotStarted,
            ExpiresAt = now.AddMinutes(Math.Max(1, _settings.ClaimTokenMinutes)),
            CreatedAt = now
        };
        var token = new RefundClaimToken
        {
            RefundClaimTokenId = NewId("RFT"),
            RefundClaimId = claim.RefundClaimId,
            TokenHash = HashToken(rawToken),
            ExpiresAt = claim.ExpiresAt,
            CreatedAt = now
        };
        claim.Tokens.Add(token);
        return new RefundClaimIssue(claim, token, rawToken);
    }

    public string HashToken(string rawToken)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)))
            .ToLowerInvariant();
    }

    private static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
}
