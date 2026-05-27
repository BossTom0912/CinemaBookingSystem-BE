using System;
using System.Collections.Generic;

namespace CinemaSystem.Infrastructure.Persistence.Models;

public partial class BookingSeat
{
    public string BookingSeatId { get; set; } = null!;

    public string BookingId { get; set; } = null!;

    public string ShowtimeSeatId { get; set; } = null!;

    public decimal SeatPrice { get; set; }

    public virtual Booking Booking { get; set; } = null!;

    public virtual ShowtimeSeat ShowtimeSeat { get; set; } = null!;

    public virtual Ticket? Ticket { get; set; }
}
