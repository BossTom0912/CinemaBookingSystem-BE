using System;
using System.Collections.Generic;

namespace CinemaSystem.Infrastructure.Persistence.Models;

public partial class Seat
{
    public string SeatId { get; set; } = null!;

    public string RoomId { get; set; } = null!;

    public string SeatTypeId { get; set; } = null!;

    public string SeatCode { get; set; } = null!;

    public string RowLabel { get; set; } = null!;

    public int SeatNumber { get; set; }

    public bool IsActive { get; set; }

    public virtual Room Room { get; set; } = null!;

    public virtual SeatType SeatType { get; set; } = null!;

    public virtual ICollection<ShowtimeSeat> ShowtimeSeats { get; set; } = new List<ShowtimeSeat>();
}
