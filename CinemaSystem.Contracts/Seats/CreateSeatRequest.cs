using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CinemaSystem.Contracts.Seats;

public sealed class CreateSeatRequest
{
    public string RoomId { get; set; } = default!;
    public string RowLabel { get; set; } = default!;
    public int SeatNumber { get; set; }
    public string SeatTypeId { get; set; } = default!;
}
