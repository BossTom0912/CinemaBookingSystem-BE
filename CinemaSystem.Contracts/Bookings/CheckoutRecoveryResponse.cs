using System;

namespace CinemaSystem.Contracts.Bookings;

/// <summary>
/// Minimal server-authoritative state used by a client to recover a checkout
/// after its create-booking response was interrupted.
/// </summary>
public sealed class CheckoutRecoveryResponse
{
    public string BookingId { get; init; } = string.Empty;
    public string ShowtimeId { get; init; } = string.Empty;
    public string BookingStatus { get; init; } = string.Empty;
    public string? PaymentStatus { get; init; }
    public DateTime? ExpiredAt { get; init; }
}
