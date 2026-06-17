using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Bookings;

public sealed class CheckoutFoodItemRequest
{
    [Required]
    [StringLength(50)]
    public string FbItemId { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }
}
