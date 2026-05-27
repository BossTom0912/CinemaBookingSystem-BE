using System;
using System.Collections.Generic;

namespace CinemaSystem.Infrastructure.Persistence.Models;

public partial class Ticket
{
    public string TicketId { get; set; } = null!;

    public string BookingSeatId { get; set; } = null!;

    public string QrCode { get; set; } = null!;

    public string TicketStatus { get; set; } = null!;

    public DateTime GeneratedAt { get; set; }

    public virtual BookingSeat BookingSeat { get; set; } = null!;

    public virtual ICollection<CheckinLog> CheckinLogs { get; set; } = new List<CheckinLog>();
}
