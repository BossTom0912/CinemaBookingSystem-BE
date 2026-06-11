using CinemaSystem.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Infrastructure.Persistence;

public sealed class CinemaDiagnosticsService : ICinemaDiagnosticsService
{
    private readonly CinemaDbContext _dbContext;

    public CinemaDiagnosticsService(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> GetMoviesCountAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Movies.CountAsync(cancellationToken);
    }
}
