using System;
using System.Collections.Generic;

namespace CinemaSystem.Domain.Entities;

public partial class AuditLog
{
    public string AuditLogId { get; set; } = null!;

    public string? UserId { get; set; }

    public string Action { get; set; } = null!;

    public string EntityName { get; set; } = null!;

    public string? EntityId { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? CorrelationId { get; set; }

    public virtual User? User { get; set; }
}
