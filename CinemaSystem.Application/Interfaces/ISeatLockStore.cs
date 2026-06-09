namespace CinemaSystem.Application.Interfaces;

public interface ISeatLockStore
{
    Task<bool> TryLockAsync(
        string lockKey,
        string userId,
        TimeSpan ttl,
        CancellationToken cancellationToken);

    Task ReleaseAsync(
        string lockKey,
        CancellationToken cancellationToken);
}
