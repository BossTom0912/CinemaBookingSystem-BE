using System;
using System.Collections.Generic;

namespace CinemaSystem.Domain.Entities;

public partial class RefreshToken
{
    public string RefreshTokenId { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public string TokenHash { get; set; } = null!;

    public DateTime IssuedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public bool IsRevoked { get; set; }

    public virtual User User { get; set; } = null!;
}
