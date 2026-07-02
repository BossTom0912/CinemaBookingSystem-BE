using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Genres;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Infrastructure.Services;

public class GenreService : IGenreService
{
    private readonly CinemaDbContext _dbContext;

    public GenreService(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<GenreResponse>> GetAllGenresAsync()
    {
        var genres = await _dbContext.Genres
            .Select(g => new GenreResponse
            {
                GenreId = g.GenreId,
                Name = g.Name
            })
            .ToListAsync();

        return genres;
    }
}
