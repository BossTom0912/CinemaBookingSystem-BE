using System;
using System.Collections.Generic;

namespace CinemaSystem.Domain.Entities;

public partial class Notification
{
    public string NotificationId { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public string? BookingId { get; set; }

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Booking? Booking { get; set; }

    public virtual User User { get; set; } = null!;
}
