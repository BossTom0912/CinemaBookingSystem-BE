using System;
using System.Collections.Concurrent;
using CinemaSystem.Application.Interfaces;

namespace CinemaSystem.Infrastructure.Services;

public sealed class UserHeartbeatTracker : IUserHeartbeatTracker
{
    private static readonly TimeSpan OnlineThreshold = TimeSpan.FromSeconds(30);
    private readonly ConcurrentDictionary<string, DateTime> _lastActiveMap = new(StringComparer.OrdinalIgnoreCase);

    public void RecordHeartbeat(string userId, string? email = null)
    {
        var now = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            _lastActiveMap[userId.Trim()] = now;
        }
        if (!string.IsNullOrWhiteSpace(email))
        {
            _lastActiveMap[email.Trim()] = now;
        }
    }

    public bool IsUserOnline(string userId, string? email = null)
    {
        var now = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(userId) && _lastActiveMap.TryGetValue(userId.Trim(), out var t1))
        {
            if (now - t1 <= OnlineThreshold) return true;
        }

        if (!string.IsNullOrWhiteSpace(email) && _lastActiveMap.TryGetValue(email.Trim(), out var t2))
        {
            if (now - t2 <= OnlineThreshold) return true;
        }

        return false;
    }

    public DateTime? GetLastActiveAt(string userId, string? email = null)
    {
        if (!string.IsNullOrWhiteSpace(userId) && _lastActiveMap.TryGetValue(userId.Trim(), out var t1))
        {
            return t1;
        }

        if (!string.IsNullOrWhiteSpace(email) && _lastActiveMap.TryGetValue(email.Trim(), out var t2))
        {
            return t2;
        }

        return null;
    }
}
