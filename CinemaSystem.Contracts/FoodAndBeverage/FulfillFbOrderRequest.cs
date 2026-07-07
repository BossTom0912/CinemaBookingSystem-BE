using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.FoodAndBeverage;

public sealed class FulfillFbOrderRequest
{
    [Required]
    public string BookingId { get; init; } = string.Empty;
}

public sealed class FbFulfillmentResponse
{
    public string BookingId { get; init; } = string.Empty;

    public string FbFulfillmentStatus { get; init; } = string.Empty;

    public DateTime? FbFulfilledAt { get; init; }

    public string? StaffProfileId { get; init; }

    public string Message { get; init; } = string.Empty;
}
