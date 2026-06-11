using System;
using System.Collections.Generic;

namespace CinemaSystem.Domain.Entities;

public partial class ShowtimeCancellation
{
    public string ShowtimeCancellationId { get; set; } = null!;

    public string ShowtimeId { get; set; } = null!;

    public string? CancelledByStaffId { get; set; }

    public string CancelReason { get; set; } = null!;

    public DateTime CancelledAt { get; set; }

    public string CancelledByUserId { get; set; } = null!;

    public virtual StaffProfile? CancelledByStaff { get; set; }

    public virtual User CancelledByUser { get; set; } = null!;

    public virtual ICollection<Refund> Refunds { get; set; } = new List<Refund>();

    public virtual Showtime Showtime { get; set; } = null!;
}
