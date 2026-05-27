using System;
using System.Collections.Generic;

namespace CinemaSystem.Infrastructure.Persistence.Models;

public partial class SeatType
{
    public string SeatTypeId { get; set; } = null!;

    public string TypeName { get; set; } = null!;

    public decimal ExtraFee { get; set; }

    public virtual ICollection<Seat> Seats { get; set; } = new List<Seat>();
}
