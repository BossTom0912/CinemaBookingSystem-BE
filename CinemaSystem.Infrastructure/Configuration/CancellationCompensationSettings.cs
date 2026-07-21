namespace CinemaSystem.Infrastructure.Configuration;

public sealed class CancellationCompensationSettings
{
    public const string SectionName = "CancellationCompensationSettings";

    public int ValidityDays { get; set; } = 180;

    public string ComboDisplayName { get; set; } =
        CinemaSystem.Domain.Constants.DomainConstants.CancellationCompensationPolicy.ComboDisplayName;
}
