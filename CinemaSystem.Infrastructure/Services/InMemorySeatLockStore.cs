using System.Collections.Concurrent;
using CinemaSystem.Application.Interfaces;

namespace CinemaSystem.Infrastructure.Services;

public sealed class InMemorySeatLockStore : ISeatLockStore
{
    private readonly ConcurrentDictionary<string, LockEntry> _locks = new();

    public Task<bool> TryLockAsync(
        string lockKey,
        string userId,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var entry = new LockEntry(userId, now.Add(ttl));

        while (true)
        {
            if (!_locks.TryGetValue(lockKey, out var existing))
            {
                if (_locks.TryAdd(lockKey, entry))
                {
                    return Task.FromResult(true);
                }

                continue;
            }

            if (existing.ExpiresAt > now)
            {
                return Task.FromResult(false);
            }

            if (_locks.TryUpdate(lockKey, entry, existing))
            {
                return Task.FromResult(true);
            }
        }
    }

    public Task ReleaseAsync(
        string lockKey,
        CancellationToken cancellationToken)
    {
        _locks.TryRemove(lockKey, out _);
        return Task.CompletedTask;
    }

    private sealed record LockEntry(string UserId, DateTime ExpiresAt);
}
