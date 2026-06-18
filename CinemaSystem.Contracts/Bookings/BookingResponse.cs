using System;
using System.Collections.Generic;

namespace CinemaSystem.Contracts.Bookings;

public sealed class BookingResponse
{
    public string BookingId { get; init; } = string.Empty;
    public string ShowtimeId { get; init; } = string.Empty;
    public string MovieTitle { get; init; } = string.Empty;
    public string CinemaName { get; init; } = string.Empty;
    public string RoomName { get; init; } = string.Empty;
    public DateTime? StartTime { get; init; }
    public decimal TotalAmount { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiredAt { get; init; }
}
