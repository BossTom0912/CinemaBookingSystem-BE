using System;
using System.Collections.Generic;

namespace CinemaSystem.Domain.Entities;

public partial class EmailVerificationToken
{
    public string TokenId { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public string Token { get; set; } = null!;

    public DateTime ExpiredAt { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public bool IsUsed { get; set; }

    public DateTime CreatedAt { get; set; }

    public string Purpose { get; set; } = null!;

    public int AttemptCount { get; set; }

    public virtual User User { get; set; } = null!;
}
