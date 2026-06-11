namespace CinemaSystem.Application.Interfaces;

public interface IDatabaseMaintenanceService
{
    Task MigrateAsync(CancellationToken cancellationToken = default);

    Task SeedAsync(bool isDevelopment, CancellationToken cancellationToken = default);
}
