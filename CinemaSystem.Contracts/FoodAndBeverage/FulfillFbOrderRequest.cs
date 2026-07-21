using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.FoodAndBeverage;

public sealed class FulfillFbOrderRequest
{
    [Required]
    public string BookingId { get; init; } = string.Empty;
}

public sealed class CounterFbOrderItemResponse
{
    public string FbItemId { get; init; } = string.Empty;

    public string ItemName { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public decimal UnitPrice { get; init; }

    public decimal ExtraFee { get; init; }

    public decimal Subtotal { get; init; }

    public List<FbItemOptionRequest> Options { get; init; } = new();
}

public sealed class FbFulfillmentResponse
{
    public string BookingId { get; init; } = string.Empty;

    public string CinemaId { get; init; } = string.Empty;

    public string? ShiftId { get; init; }

    public string? CustomerProfileId { get; init; }

    public string? GuestName { get; init; }

    public string? GuestPhone { get; init; }

    public decimal GrossAmount { get; init; }

    public decimal DiscountAmount { get; init; }

    public string? VoucherCode { get; init; }

    public decimal TotalAmount { get; init; }

    public string PaymentMethod { get; init; } = "CASH";

    public decimal? ReceivedAmount { get; init; }

    public decimal? ChangeAmount { get; init; }

    public string FbFulfillmentStatus { get; init; } = string.Empty;

    public DateTime? FbFulfilledAt { get; init; }

    public string? StaffProfileId { get; init; }

    public List<CounterFbOrderItemResponse> Items { get; init; } = new();

    public string Message { get; init; } = string.Empty;
}
