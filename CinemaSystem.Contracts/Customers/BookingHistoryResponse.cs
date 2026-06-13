using System;
using System.Collections.Generic;

namespace CinemaSystem.Contracts.Customers;

public sealed class BookingHistoryResponse
{
    public string BookingId { get; init; } = string.Empty;
    public string ShowtimeId { get; init; } = string.Empty;
    public string MovieTitle { get; init; } = string.Empty;
    public string? MoviePosterUrl { get; init; }
    public string CinemaName { get; init; } = string.Empty;
    public string RoomName { get; init; } = string.Empty;
    public DateTime StartTime { get; init; }
    public decimal TotalAmount { get; init; }
    public string BookingStatus { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public List<BookedSeatResponse> Seats { get; init; } = new();
}

public sealed class BookedSeatResponse
{
    public string SeatId { get; init; } = string.Empty;
    public string SeatNumber { get; init; } = string.Empty;
    public string Row { get; init; } = string.Empty;
    public string SeatType { get; init; } = string.Empty;
}
