using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.FoodAndBeverage;

public sealed class FbOrderItemRequest
{
    [Required]
    public string FbItemId { get; init; } = string.Empty;

    [Range(1, 100)]
    public int Quantity { get; init; }
}

public sealed class CreateCounterFbOrderRequest
{
    [Required]
    public string CinemaId { get; init; } = string.Empty;

    public string? ShowtimeId { get; init; }

    public string? CustomerProfileId { get; init; }

    public string? GuestName { get; init; }

    public string? GuestPhone { get; init; }

    public string? GuestEmail { get; init; }

    [Required]
    [MinLength(1, ErrorMessage = "At least one F&B item must be selected.")]
    public List<FbOrderItemRequest> Items { get; init; } = new();
}
