namespace CinemaSystem.Contracts.Seats;

public sealed class SeatTypeResponse
{
    public string SeatTypeId { get; init; } = string.Empty;

    public string TypeName { get; init; } = string.Empty;

    public decimal ExtraFee { get; init; }

    public int SeatSpan { get; init; }

    public bool IsActive { get; init; }

    public int SortOrder { get; init; }
}
