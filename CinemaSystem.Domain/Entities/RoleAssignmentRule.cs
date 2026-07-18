using System;

namespace CinemaSystem.Domain.Entities;

/// <summary>
/// Defines which role may provision which target role.
/// </summary>
public partial class RoleAssignmentRule
{
    public string GrantorRoleId { get; set; } = null!;

    public string GranteeRoleId { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual Role GrantorRole { get; set; } = null!;

    public virtual Role GranteeRole { get; set; } = null!;
}
