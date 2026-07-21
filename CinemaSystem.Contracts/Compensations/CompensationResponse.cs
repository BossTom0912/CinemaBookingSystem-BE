using System;
using System.Collections.Generic;

namespace CinemaSystem.Contracts.Compensations;

public sealed class CompensationResponse
{
    public string CompensationId { get; init; } = string.Empty;

    public string SourceBookingId { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string PolicyVersion { get; init; } = string.Empty;

    public DateTime IssuedAt { get; init; }

    public DateTime ExpiresAt { get; init; }

    public IReadOnlyList<CompensationTicketResponse> Tickets { get; init; } =
        Array.Empty<CompensationTicketResponse>();

    public CompensationComboResponse? Combo { get; init; }
}

public sealed class CompensationTicketResponse
{
    public string CompensationTicketId { get; init; } = string.Empty;

    public string VoucherCode { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? ReservedBookingId { get; init; }

    public DateTime? RedeemedAt { get; init; }
}

public sealed class CompensationComboResponse
{
    public string CompensationComboId { get; init; } = string.Empty;

    public string VoucherCode { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateTime? RedeemedAt { get; init; }

    public string? RedeemedAtCinemaId { get; init; }
}
