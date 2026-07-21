using System;

namespace CinemaSystem.Domain.Entities;

public class CompensationTicket
{
    public string CompensationTicketId { get; set; } = null!;

    public string CancellationCompensationId { get; set; } = null!;

    public string VoucherCode { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? ReservedBookingId { get; set; }

    public string? ReservedBookingSeatId { get; set; }

    public DateTime? ReservedAt { get; set; }

    public DateTime? RedeemedAt { get; set; }

    public byte[]? RowVersion { get; set; }

    public virtual CancellationCompensation CancellationCompensation { get; set; } = null!;

    public virtual Booking? ReservedBooking { get; set; }

    public virtual BookingSeat? ReservedBookingSeat { get; set; }
}
