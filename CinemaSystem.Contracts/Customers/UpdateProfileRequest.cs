namespace CinemaSystem.Contracts.Customers;

public sealed class UpdateProfileRequest
{
    public string? FullName { get; init; }
    public string? Address { get; init; }
    public string? AvatarUrl { get; init; }
    public string? Gender { get; init; }
    public DateOnly? DateOfBirth { get; init; }
}
