using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.FoodAndBeverage;

public sealed class UpdateFbItemRequest
{
    [Required]
    [StringLength(255)]
    public string ItemName { get; init; } = string.Empty;

    [Range(0, 100000000)]
    public decimal Price { get; init; }

    [Required]
    [StringLength(30)]
    public string ItemStatus { get; init; } = "AVAILABLE";
}
