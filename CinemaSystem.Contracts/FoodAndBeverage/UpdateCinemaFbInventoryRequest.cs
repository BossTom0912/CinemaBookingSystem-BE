using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.FoodAndBeverage;

public sealed class UpdateCinemaFbInventoryRequest
{
    [Required]
    public string CinemaId { get; init; } = string.Empty;

    [Required]
    public string FbItemId { get; init; } = string.Empty;

    [Range(0, 1000000)]
    public int Quantity { get; init; }
}
