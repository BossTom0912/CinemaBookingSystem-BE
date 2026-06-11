using System;
using System.Collections.Generic;

namespace CinemaSystem.Domain.Entities;

public partial class User
{
    public string UserId { get; set; } = null!;

    public string RoleId { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public string Status { get; set; } = null!;

    public bool EmailVerified { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual CustomerProfile? CustomerProfile { get; set; }

    public virtual ICollection<EmailVerificationToken> EmailVerificationTokens { get; set; } = new List<EmailVerificationToken>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public virtual Role Role { get; set; } = null!;

    public virtual ICollection<ShowtimeCancellation> ShowtimeCancellations { get; set; } = new List<ShowtimeCancellation>();

    public virtual ICollection<ShowtimeSeat> ShowtimeSeats { get; set; } = new List<ShowtimeSeat>();

    public virtual StaffProfile? StaffProfile { get; set; }
}
