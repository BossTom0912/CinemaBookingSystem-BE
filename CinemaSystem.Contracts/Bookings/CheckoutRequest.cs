using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Bookings;

public sealed class CheckoutRequest : IValidatableObject
{
    [Required]
    [StringLength(50)]
    public string ShowtimeId { get; init; } = string.Empty;

    [Required]
    [MinLength(1)]
    public IReadOnlyList<string> ShowtimeSeatIds { get; init; } = [];

    public IReadOnlyList<CheckoutFoodItemRequest> FoodItems { get; init; } = [];

    [StringLength(100)]
    public string? VoucherCode { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var seatIds = ShowtimeSeatIds ?? [];
        if (seatIds.Any(string.IsNullOrWhiteSpace))
        {
            yield return new ValidationResult(
                "Showtime seat IDs must not be empty.",
                [nameof(ShowtimeSeatIds)]);
        }

        if (ContainsDuplicates(seatIds))
        {
            yield return new ValidationResult(
                "Showtime seat IDs must not contain duplicates.",
                [nameof(ShowtimeSeatIds)]);
        }

        var foodItems = FoodItems ?? [];
        if (foodItems.Any(item => item is null || string.IsNullOrWhiteSpace(item.FbItemId)))
        {
            yield return new ValidationResult(
                "Food item IDs must not be empty.",
                [nameof(FoodItems)]);
        }

        if (ContainsDuplicates(foodItems.Where(item => item is not null).Select(item => item.FbItemId)))
        {
            yield return new ValidationResult(
                "Food item IDs must not contain duplicates.",
                [nameof(FoodItems)]);
        }

        if (VoucherCode is not null && string.IsNullOrWhiteSpace(VoucherCode))
        {
            yield return new ValidationResult(
                "Voucher code must not be empty when provided.",
                [nameof(VoucherCode)]);
        }
    }

    private static bool ContainsDuplicates(IEnumerable values)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            if (value is string text && !seen.Add(text.Trim()))
            {
                return true;
            }
        }

        return false;
    }
}
