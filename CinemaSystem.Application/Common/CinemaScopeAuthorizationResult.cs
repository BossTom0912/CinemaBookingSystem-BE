namespace CinemaSystem.Application.Common;

public sealed record CinemaScopeAuthorizationResult(
    bool Allowed,
    int StatusCode,
    string Message,
    string? ErrorCode,
    string? CinemaId = null)
{
    public static CinemaScopeAuthorizationResult Allow(string? cinemaId = null)
    {
        return new CinemaScopeAuthorizationResult(true, 200, string.Empty, null, cinemaId);
    }

    public static CinemaScopeAuthorizationResult Deny(
        int statusCode,
        string message,
        string errorCode)
    {
        return new CinemaScopeAuthorizationResult(false, statusCode, message, errorCode);
    }
}
