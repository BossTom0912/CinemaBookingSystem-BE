using System;
using System.Collections.Generic;

namespace CinemaSystem.Infrastructure.Persistence.Models;

public partial class Room
{
    public string RoomId { get; set; } = null!;

    public string CinemaId { get; set; } = null!;

    public string RoomName { get; set; } = null!;

    public int Capacity { get; set; }

    public string RoomStatus { get; set; } = null!;

    public virtual Cinema Cinema { get; set; } = null!;

    public virtual ICollection<Seat> Seats { get; set; } = new List<Seat>();

    public virtual ICollection<Showtime> Showtimes { get; set; } = new List<Showtime>();
}
