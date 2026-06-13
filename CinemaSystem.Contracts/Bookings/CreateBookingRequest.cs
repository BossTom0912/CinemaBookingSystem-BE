using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Bookings;

public sealed class CreateBookingRequest
{
    [Required]
    public string ShowtimeId { get; init; } = string.Empty;

    [Required]
    [MinLength(1, ErrorMessage = "At least one seat must be selected.")]
    public List<string> ShowtimeSeatIds { get; init; } = new();

    public string? VoucherCode { get; init; }

    public List<BookingFbItemRequest>? FoodAndBeverages { get; init; }
}

public sealed class BookingFbItemRequest
{
    public string FbItemId { get; init; } = string.Empty;
    public int Quantity { get; init; }
}
