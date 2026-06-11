namespace CinemaSystem.Application.Interfaces;

public interface ICinemaDiagnosticsService
{
    Task<int> GetMoviesCountAsync(CancellationToken cancellationToken);
}
