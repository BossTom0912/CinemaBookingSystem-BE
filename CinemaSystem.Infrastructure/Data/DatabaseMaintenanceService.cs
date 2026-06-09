using CinemaSystem.Application.Interfaces;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Infrastructure.Data;

public sealed class DatabaseMaintenanceService : IDatabaseMaintenanceService
{
    private readonly CinemaDbContext _dbContext;
    private readonly IServiceProvider _serviceProvider;

    public DatabaseMaintenanceService(CinemaDbContext dbContext, IServiceProvider serviceProvider)
    {
        _dbContext = dbContext;
        _serviceProvider = serviceProvider;
    }

    public Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.Database.MigrateAsync(cancellationToken);
    }

    public Task SeedAsync(bool isDevelopment, CancellationToken cancellationToken = default)
    {
        return DbInitializer.SeedAsync(_serviceProvider, isDevelopment);
    }
}
