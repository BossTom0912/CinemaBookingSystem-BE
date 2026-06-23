using System.Security.Claims;
using CinemaSystem.Application.Common;

namespace CinemaSystem.Application.Interfaces;

public interface ICinemaScopeAuthorizationService
{
    Task<CinemaScopeAuthorizationResult> GetUserCinemaScopeAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken);

    Task<CinemaScopeAuthorizationResult> AuthorizeCinemaAsync(
        ClaimsPrincipal user,
        string cinemaId,
        CancellationToken cancellationToken);

    Task<CinemaScopeAuthorizationResult> AuthorizeRoomAsync(
        ClaimsPrincipal user,
        string roomId,
        CancellationToken cancellationToken);

    Task<CinemaScopeAuthorizationResult> AuthorizeSeatAsync(
        ClaimsPrincipal user,
        string seatId,
        CancellationToken cancellationToken);

    Task<CinemaScopeAuthorizationResult> AuthorizeShowtimeAsync(
        ClaimsPrincipal user,
        string showtimeId,
        CancellationToken cancellationToken);
}
