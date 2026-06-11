using System;
using System.Collections.Generic;

namespace CinemaSystem.Domain.Entities;

public partial class ShowtimeSeat
{
    public string ShowtimeSeatId { get; set; } = null!;

    public string ShowtimeId { get; set; } = null!;

    public string SeatId { get; set; } = null!;

    public string SeatStatus { get; set; } = null!;

    public DateTime? LockedUntil { get; set; }

    public string? LockedByUserId { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual BookingSeat? BookingSeat { get; set; }

    public virtual User? LockedByUser { get; set; }

    public virtual Seat Seat { get; set; } = null!;

    public virtual Showtime Showtime { get; set; } = null!;
}
