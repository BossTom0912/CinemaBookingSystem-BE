using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Infrastructure.Auth;

public sealed class CinemaScopeAuthorizationService : ICinemaScopeAuthorizationService
{
    private const string ScopeForbiddenMessage = "You do not have permission to access this cinema resource.";
    private const string ScopeForbiddenCode = "CINEMA_SCOPE_FORBIDDEN";

    private readonly CinemaDbContext _dbContext;

    public CinemaScopeAuthorizationService(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CinemaScopeAuthorizationResult> GetUserCinemaScopeAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (IsInRole(user, AuthConstants.Roles.Admin))
        {
            return CinemaScopeAuthorizationResult.Allow();
        }

        if (!IsInRole(user, AuthConstants.Roles.Manager)
            && !IsInRole(user, AuthConstants.Roles.Staff))
        {
            return CinemaScopeAuthorizationResult.Deny(
                403,
                ScopeForbiddenMessage,
                ScopeForbiddenCode);
        }

        var userId = GetUserId(user);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return CinemaScopeAuthorizationResult.Deny(
                401,
                "User is required.",
                "USER_REQUIRED");
        }

        var cinemaId = await _dbContext.StaffProfiles
            .AsNoTracking()
            .Where(profile =>
                profile.UserId == userId
                && profile.EmploymentStatus == BookingConstants.ResourceStatus.Active)
            .Select(profile => profile.CinemaId)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(cinemaId))
        {
            return CinemaScopeAuthorizationResult.Deny(
                403,
                "Active staff profile cinema scope was not found.",
                "STAFF_PROFILE_SCOPE_NOT_FOUND");
        }

        return CinemaScopeAuthorizationResult.Allow(cinemaId);
    }

    public async Task<CinemaScopeAuthorizationResult> AuthorizeCinemaAsync(
        ClaimsPrincipal user,
        string cinemaId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cinemaId))
        {
            return CinemaScopeAuthorizationResult.Deny(
                400,
                "Cinema ID is required.",
                "CINEMA_ID_REQUIRED");
        }

        var scope = await GetUserCinemaScopeAsync(user, cancellationToken);
        if (!scope.Allowed)
        {
            return scope;
        }

        if (scope.CinemaId is null
            || string.Equals(scope.CinemaId, cinemaId, StringComparison.OrdinalIgnoreCase))
        {
            return CinemaScopeAuthorizationResult.Allow(scope.CinemaId);
        }

        return CinemaScopeAuthorizationResult.Deny(
            403,
            ScopeForbiddenMessage,
            ScopeForbiddenCode);
    }

    public async Task<CinemaScopeAuthorizationResult> AuthorizeRoomAsync(
        ClaimsPrincipal user,
        string roomId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return CinemaScopeAuthorizationResult.Deny(
                400,
                "Room ID is required.",
                "ROOM_ID_REQUIRED");
        }

        var cinemaId = await _dbContext.Rooms
            .AsNoTracking()
            .Where(room => room.RoomId == roomId)
            .Select(room => room.CinemaId)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(cinemaId))
        {
            return CinemaScopeAuthorizationResult.Deny(
                404,
                "Room was not found.",
                "ROOM_NOT_FOUND");
        }

        return await AuthorizeCinemaAsync(user, cinemaId, cancellationToken);
    }

    public async Task<CinemaScopeAuthorizationResult> AuthorizeSeatAsync(
        ClaimsPrincipal user,
        string seatId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(seatId))
        {
            return CinemaScopeAuthorizationResult.Deny(
                400,
                "Seat ID is required.",
                "SEAT_ID_REQUIRED");
        }

        var cinemaId = await _dbContext.Seats
            .AsNoTracking()
            .Where(seat => seat.SeatId == seatId)
            .Select(seat => seat.Room.CinemaId)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(cinemaId))
        {
            return CinemaScopeAuthorizationResult.Deny(
                404,
                "Seat was not found.",
                "SEAT_NOT_FOUND");
        }

        return await AuthorizeCinemaAsync(user, cinemaId, cancellationToken);
    }

    public async Task<CinemaScopeAuthorizationResult> AuthorizeShowtimeAsync(
        ClaimsPrincipal user,
        string showtimeId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(showtimeId))
        {
            return CinemaScopeAuthorizationResult.Deny(
                400,
                "Showtime ID is required.",
                "SHOWTIME_ID_REQUIRED");
        }

        var cinemaId = await _dbContext.Showtimes
            .AsNoTracking()
            .Where(showtime => showtime.ShowtimeId == showtimeId)
            .Select(showtime => showtime.Room.CinemaId)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(cinemaId))
        {
            return CinemaScopeAuthorizationResult.Deny(
                404,
                "Showtime was not found.",
                "SHOWTIME_NOT_FOUND");
        }

        return await AuthorizeCinemaAsync(user, cinemaId, cancellationToken);
    }

    private static string GetUserId(ClaimsPrincipal user)
    {
        return user.FindFirst(AuthConstants.Claims.UserId)?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? string.Empty;
    }

    private static bool IsInRole(ClaimsPrincipal user, string role)
    {
        if (user.IsInRole(role))
        {
            return true;
        }

        return user.Claims.Any(claim =>
            (claim.Type == ClaimTypes.Role || claim.Type == "role")
            && AuthConstants.Roles.Normalize(claim.Value) == role);
    }
}
