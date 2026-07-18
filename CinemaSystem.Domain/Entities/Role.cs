using System;
using System.Collections.Generic;

namespace CinemaSystem.Domain.Entities;

public partial class Role
{
    public string RoleId { get; set; } = null!;

    public string RoleName { get; set; } = null!;

    public string? Description { get; set; }

    public virtual RoleProvisioningPolicy? ProvisioningPolicy { get; set; }

    public virtual ICollection<RoleAssignmentRule> GrantedAssignmentRules { get; set; } = new List<RoleAssignmentRule>();

    public virtual ICollection<RoleAssignmentRule> ReceivedAssignmentRules { get; set; } = new List<RoleAssignmentRule>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
