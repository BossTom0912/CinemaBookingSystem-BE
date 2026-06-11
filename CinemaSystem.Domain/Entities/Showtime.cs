using System;
using System.Collections.Generic;

namespace CinemaSystem.Domain.Entities;

public partial class Showtime
{
    public string ShowtimeId { get; set; } = null!;

    public string MovieId { get; set; } = null!;

    public string RoomId { get; set; } = null!;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public decimal BasePrice { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public virtual Movie Movie { get; set; } = null!;

    public virtual Room Room { get; set; } = null!;

    public virtual ShowtimeCancellation? ShowtimeCancellation { get; set; }

    public virtual ICollection<ShowtimeSeat> ShowtimeSeats { get; set; } = new List<ShowtimeSeat>();
}
