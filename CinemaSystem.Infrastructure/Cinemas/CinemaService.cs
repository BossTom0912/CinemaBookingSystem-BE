using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Cinemas;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CinemaSystem.Infrastructure.Cinemas;

public sealed class CinemaService : ICinemaService
{
    private readonly CinemaDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private const string CacheKey = "Master_Cinemas_List";

    public CinemaService(CinemaDbContext dbContext, IMemoryCache? cache = null)
    {
        _dbContext = dbContext;
        _cache = cache ?? new MemoryCache(new MemoryCacheOptions());
    }

    public async Task<ServiceResult<IReadOnlyList<CinemaResponse>>> GetCinemasAsync(
        CancellationToken cancellationToken)
    {
        var cached = await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
            return await _dbContext.Cinemas
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
        }) ?? new List<CinemaResponse>();

        return ServiceResult<IReadOnlyList<CinemaResponse>>.Ok(
            cached,
            "Cinemas retrieved successfully.");
    }
}
