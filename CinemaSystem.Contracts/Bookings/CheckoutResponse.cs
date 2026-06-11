namespace CinemaSystem.Contracts.Bookings;

public sealed class CheckoutResponse
{
    public string BookingId { get; init; } = string.Empty;

    public string BookingStatus { get; init; } = string.Empty;

    public string ShowtimeId { get; init; } = string.Empty;

    public IReadOnlyList<CheckoutSeatResponse> Seats { get; init; } = [];

    public IReadOnlyList<CheckoutFoodItemResponse> FoodItems { get; init; } = [];

    public decimal SeatSubtotal { get; init; }

    public decimal FoodSubtotal { get; init; }

    public decimal GrossAmount { get; init; }

    public decimal VoucherDiscount { get; init; }

    public decimal RewardDiscount { get; init; }

    public decimal TotalAmount { get; init; }

    public DateTime ExpiredAt { get; init; }
}

public sealed class CheckoutSeatResponse
{
    public string ShowtimeSeatId { get; init; } = string.Empty;

    public string SeatCode { get; init; } = string.Empty;

    public string SeatType { get; init; } = string.Empty;

    public decimal Price { get; init; }
}

public sealed class CheckoutFoodItemResponse
{
    public string FbItemId { get; init; } = string.Empty;

    public string ItemName { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public decimal UnitPrice { get; init; }

    public decimal Subtotal { get; init; }
}
