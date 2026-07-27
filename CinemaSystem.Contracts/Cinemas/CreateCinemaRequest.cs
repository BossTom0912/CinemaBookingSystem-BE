namespace CinemaSystem.Contracts.Cinemas;

public sealed class CreateCinemaRequest
{
    public string CinemaName { get; init; } = string.Empty;

    public string Address { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string? PhoneNumber { get; init; }

    public string? CinemaStatus { get; init; } = "ACTIVE";
}
