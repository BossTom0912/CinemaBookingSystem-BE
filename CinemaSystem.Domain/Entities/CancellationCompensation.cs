using System;
using System.Collections.Generic;

namespace CinemaSystem.Domain.Entities;

public class CancellationCompensation
{
    public string CancellationCompensationId { get; set; } = null!;

    public string SourceBookingId { get; set; } = null!;

    public string ShowtimeCancellationId { get; set; } = null!;

    public string? CustomerProfileId { get; set; }

    public string Status { get; set; } = null!;

    public string PolicyVersion { get; set; } = null!;

    public DateTime IssuedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public virtual Booking SourceBooking { get; set; } = null!;

    public virtual ShowtimeCancellation ShowtimeCancellation { get; set; } = null!;

    public virtual CustomerProfile? CustomerProfile { get; set; }

    public virtual ICollection<CompensationTicket> Tickets { get; set; } = new List<CompensationTicket>();

    public virtual CompensationCombo? Combo { get; set; }
}
