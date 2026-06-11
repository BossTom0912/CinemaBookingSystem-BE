namespace CinemaSystem.Contracts.Customers;

public sealed class UpdateEmailRequest
{
    public string NewEmail { get; init; } = string.Empty;
}
