using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Tickets;

public sealed class ConfirmTicketScanRequest
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(TicketContractConstants.EntityIdMaxLength)]
    public string TicketId { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [MaxLength(TicketContractConstants.EntityIdMaxLength)]
    public string RoomId { get; init; } = string.Empty;
}
