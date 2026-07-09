using CinemaSystem.Domain.Entities;

namespace CinemaSystem.Application.Common;

public sealed record RefundClaimIssue(
    RefundClaim Claim,
    RefundClaimToken Token,
    string RawToken);
