using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Bookings;

public sealed class CreateCounterBookingRequest
{
    [Required]
    public string ShowtimeId { get; init; } = string.Empty;

    [Required]
    [MinLength(1, ErrorMessage = "At least one seat must be selected.")]
    public List<string> ShowtimeSeatIds { get; init; } = new();

    public string? VoucherCode { get; init; }

    public List<string>? CompensationTicketCodes { get; init; }

    public List<BookingFbItemRequest>? FoodAndBeverages { get; init; }

    public string? CustomerProfileId { get; init; }

    public string? GuestName { get; init; }

    public string? GuestPhone { get; init; }

    public string? GuestEmail { get; init; }
}
