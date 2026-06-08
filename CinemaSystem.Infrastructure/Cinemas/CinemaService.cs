using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Cinemas;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Infrastructure.Cinemas;

public sealed class CinemaService : ICinemaService
{
    private readonly CinemaDbContext _dbContext;

    public CinemaService(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<IReadOnlyList<CinemaResponse>>> GetCinemasAsync(
        CancellationToken cancellationToken)
    {
        var cinemas = await _dbContext.Cinemas
            .AsNoTracking()
            .OrderBy(cinema => cinema.CinemaName)
            .Select(cinema => new CinemaResponse
            {
                CinemaId = cinema.CinemaId,
                CinemaName = cinema.CinemaName,
                Address = cinema.Address,
                City = cinema.City,
                PhoneNumber = cinema.PhoneNumber,
                CinemaStatus = cinema.CinemaStatus
            })
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<CinemaResponse>>.Ok(cinemas, "Cinemas retrieved successfully.");
    }
}
