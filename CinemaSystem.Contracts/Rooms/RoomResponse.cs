namespace CinemaSystem.Contracts.Rooms;

public sealed class RoomResponse
{
    public string RoomId { get; init; } = string.Empty;

    public string CinemaId { get; init; } = string.Empty;

    public string CinemaName { get; init; } = string.Empty;

    public string RoomName { get; init; } = string.Empty;

    public int Capacity { get; init; }

    public string RoomStatus { get; init; } = string.Empty;

    public int SeatCount { get; init; }
}
