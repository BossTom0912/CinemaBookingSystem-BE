using System;

namespace CinemaSystem.Domain.Entities;

/// <summary>
/// Data-driven account-provisioning metadata for a system role.
/// </summary>
public partial class RoleProvisioningPolicy
{
    public string RoleId { get; set; } = null!;

    public string ProfileKind { get; set; } = null!;

    public bool RequiresCinema { get; set; }

    public string? DefaultStaffPosition { get; set; }

    public bool IsActive { get; set; }

    public bool IsPublicRegistrationAllowed { get; set; }

    public virtual Role Role { get; set; } = null!;
}
