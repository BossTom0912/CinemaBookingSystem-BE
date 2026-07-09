namespace CinemaSystem.Contracts.FoodAndBeverage;

public sealed class CinemaFbInventoryResponse
{
    public string CinemaInventoryId { get; init; } = string.Empty;

    public string CinemaId { get; init; } = string.Empty;

    public string FbItemId { get; init; } = string.Empty;

    public string ItemName { get; init; } = string.Empty;

    public decimal Price { get; init; }

    public int Quantity { get; init; }
}
