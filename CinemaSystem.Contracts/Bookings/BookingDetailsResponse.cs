using System;
using System.Collections.Generic;

namespace CinemaSystem.Contracts.Bookings;

public sealed class BookingDetailsResponse
{
    public string BookingId { get; init; } = string.Empty;
    public string ShowtimeId { get; init; } = string.Empty;
    public string MovieTitle { get; init; } = string.Empty;
    public string CinemaName { get; init; } = string.Empty;
    public string RoomName { get; init; } = string.Empty;
    public DateTime StartTime { get; init; }
    public decimal TotalAmount { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    
    public List<BookedSeatDetailsResponse> Seats { get; init; } = new();
    public List<BookedFbItemResponse> FoodAndBeverages { get; init; } = new();
}

public sealed class BookedSeatDetailsResponse
{
    public string SeatId { get; init; } = string.Empty;
    public string SeatNumber { get; init; } = string.Empty;
    public string RowLabel { get; init; } = string.Empty;
    public string SeatType { get; init; } = string.Empty;
    public decimal Price { get; init; }
}

public sealed class BookedFbItemResponse
{
    public string ItemName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal Subtotal { get; init; }
}
