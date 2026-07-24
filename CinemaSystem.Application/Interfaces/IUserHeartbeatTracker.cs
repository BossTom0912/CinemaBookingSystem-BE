using System;

namespace CinemaSystem.Application.Interfaces;

public interface IUserHeartbeatTracker
{
    void RecordHeartbeat(string userId, string? email = null);
    bool IsUserOnline(string userId, string? email = null);
    DateTime? GetLastActiveAt(string userId, string? email = null);
}
