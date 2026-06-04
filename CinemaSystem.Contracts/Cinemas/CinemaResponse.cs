namespace CinemaSystem.Contracts.Cinemas;

public sealed class CinemaResponse
{
    public string CinemaId { get; init; } = string.Empty;

    public string CinemaName { get; init; } = string.Empty;

    public string Address { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string? PhoneNumber { get; init; }

    public string CinemaStatus { get; init; } = string.Empty;
}
