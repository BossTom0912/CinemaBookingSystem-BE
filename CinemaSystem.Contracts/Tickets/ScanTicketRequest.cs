using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Tickets;

public sealed class ScanTicketRequest : IValidatableObject
{
    [Required]
    [StringLength(450)]
    public string QrCode { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(QrCode))
        {
            yield return new ValidationResult(
                "QR code must not be empty.",
                [nameof(QrCode)]);
        }
    }
}
