using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.FoodAndBeverage;

public sealed class FbItemOptionRequest
{
    [Required]
    public string OptionId { get; init; } = string.Empty;

    public string? OptionName { get; init; }

    public decimal ExtraFee { get; init; }
}

public sealed class FbOrderItemRequest
{
    [Required]
    public string FbItemId { get; init; } = string.Empty;

    public string? ItemId { get => FbItemId; init => FbItemId = value ?? string.Empty; }

    [Range(1, 100)]
    public int Quantity { get; init; } = 1;

    public decimal? UnitPrice { get; init; }

    public List<FbItemOptionRequest>? Options { get; init; }
}

public sealed class CreateCounterFbOrderRequest
{
    [Required]
    public string CinemaId { get; init; } = string.Empty;

    public string? ShiftId { get; init; }

    public string? BookingId { get; init; }

    public string? ShowtimeId { get; init; }

    public string? CustomerProfileId { get; init; }

    public string? CustomerId { get => CustomerProfileId; init => CustomerProfileId = value; }

    public string? MemberCardNumber { get; init; }

    public string? GuestName { get; init; }

    public string? GuestPhone { get; init; }

    public string? GuestEmail { get; init; }

    [Required]
    [MinLength(1, ErrorMessage = "At least one F&B item must be selected.")]
    public List<FbOrderItemRequest> Items { get; init; } = new();

    public string? VoucherCode { get; init; }

    public decimal? DiscountAmount { get; init; }

    public decimal? TotalAmount { get; init; }

    public string? PaymentMethod { get; init; } = "CASH";

    public decimal? ReceivedAmount { get; init; }

    public decimal? ChangeAmount { get; init; }
}
