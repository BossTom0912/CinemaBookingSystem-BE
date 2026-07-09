namespace CinemaSystem.Contracts.Customers;

using CinemaSystem.Domain.Constants;

public sealed class CustomerProfileResponse
{
    public string UserId { get; init; } = string.Empty;
    public string CustomerProfileId { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public string? Address { get; init; }
    public string? AvatarUrl { get; init; }
    public string? Gender { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string MemberLevel { get; init; } = DomainConstants.MemberLevel.Standard;
    public int RewardPoints { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool EmailVerified { get; init; }
}
