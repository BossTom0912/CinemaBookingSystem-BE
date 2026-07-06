namespace CinemaSystem.Contracts.FoodAndBeverage;

public sealed class FbItemResponse
{
    public string FbItemId { get; init; } = string.Empty;

    public string ItemName { get; init; } = string.Empty;

    public decimal Price { get; init; }

    public string ItemStatus { get; init; } = string.Empty;
}
