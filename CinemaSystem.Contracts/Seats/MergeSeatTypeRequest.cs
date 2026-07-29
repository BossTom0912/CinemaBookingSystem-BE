namespace CinemaSystem.Contracts.Seats;

public sealed class MergeSeatTypeRequest
{
    public string ReplacementSeatTypeId { get; init; } = string.Empty;
}
