using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Genres;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CinemaSystem.Infrastructure.Services;

public class GenreService : IGenreService
{
    private readonly CinemaDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private const string CacheKey = "Master_Genres_List";

    public GenreService(CinemaDbContext dbContext, IMemoryCache? cache = null)
    {
        _dbContext = dbContext;
        _cache = cache ?? new MemoryCache(new MemoryCacheOptions());
    }

    public async Task<List<GenreResponse>> GetAllGenresAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            return await _dbContext.Genres
                .AsNoTracking()
                .Select(g => new GenreResponse
                {
                    GenreId = g.GenreId,
                    Name = g.Name
                })
                .ToListAsync();
        }) ?? new List<GenreResponse>();
    }
}
