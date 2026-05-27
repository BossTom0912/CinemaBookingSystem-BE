using System;
using System.Collections.Generic;

namespace CinemaSystem.Infrastructure.Persistence.Models;

public partial class CheckinLog
{
    public string CheckInLogId { get; set; } = null!;

    public string? TicketId { get; set; }

    public string StaffProfileId { get; set; } = null!;

    public DateTime ScanTime { get; set; }

    public string Result { get; set; } = null!;

    public string? FailureReason { get; set; }

    public string? RawQrCode { get; set; }

    public virtual StaffProfile StaffProfile { get; set; } = null!;

    public virtual Ticket? Ticket { get; set; }
}
