using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CinemaSystem.Contracts.Rooms
{
    public sealed class GenerateSeatsRequest
    {
        public int Rows { get; set; }

        public int Columns { get; set; }

        public string SeatTypeId { get; set; } = default!;
    }
}
