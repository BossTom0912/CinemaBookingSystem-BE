namespace CinemaSystem.Contracts.Seats;

/// <summary>
/// DTO for batch updating seat statuses.
/// Optimized to send multiple seat updates in a single request and process with one database call.
/// </summary>
public sealed class BatchUpdateSeatStatusRequest
{
    /// <summary>
    /// Collection of seat IDs to update.
    /// </summary>
    public IEnumerable<string> SeatIds { get; init; } = [];

    /// <summary>
    /// New status value for all specified seats.
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Optional: Room ID to validate all seats belong to the same room.
    /// </summary>
    public string? RoomId { get; init; }
}
