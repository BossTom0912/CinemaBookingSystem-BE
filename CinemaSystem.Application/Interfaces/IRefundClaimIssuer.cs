using CinemaSystem.Application.Common;

namespace CinemaSystem.Application.Interfaces;

public interface IRefundClaimIssuer
{
    RefundClaimIssue Create(string refundId, string customerProfileId, DateTime now);
    string HashToken(string rawToken);
}
