using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Tickets;

public sealed class ScanTicketRequest
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(TicketContractConstants.QrCodeMaxLength)]
    public string QrCode { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [MaxLength(TicketContractConstants.EntityIdMaxLength)]
    public string RoomId { get; init; } = string.Empty;
}
