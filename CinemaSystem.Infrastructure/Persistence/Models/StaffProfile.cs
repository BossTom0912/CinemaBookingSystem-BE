using System;
using System.Collections.Generic;

namespace CinemaSystem.Infrastructure.Persistence.Models;

public partial class StaffProfile
{
    public string StaffProfileId { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public string CinemaId { get; set; } = null!;

    public string Position { get; set; } = null!;

    public DateOnly? HireDate { get; set; }

    public string EmploymentStatus { get; set; } = null!;

    public DateOnly? DateOfBirth { get; set; }

    public string? Gender { get; set; }

    public string? IdentityCard { get; set; }

    public string? Address { get; set; }

    public string? AvatarUrl { get; set; }

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public virtual ICollection<CheckinLog> CheckinLogs { get; set; } = new List<CheckinLog>();

    public virtual Cinema Cinema { get; set; } = null!;

    public virtual ICollection<ShowtimeCancellation> ShowtimeCancellations { get; set; } = new List<ShowtimeCancellation>();

    public virtual User User { get; set; } = null!;
}
