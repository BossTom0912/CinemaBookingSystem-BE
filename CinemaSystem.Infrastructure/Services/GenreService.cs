using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Genres;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Infrastructure.Services;

/// <summary>
/// Xử lý truy vấn thể loại phim từ bảng GENRE thông qua CinemaDbContext.
/// Kết quả được ánh xạ sang GenreResponse rồi trả ngược về GenresController.
/// Nếu cần thay đổi quy tắc lọc/sắp xếp thể loại, bắt đầu tại GetAllGenresAsync.
/// </summary>
public class GenreService : IGenreService
{
    private readonly CinemaDbContext _dbContext;

    public GenreService(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<GenreResponse>> GetAllGenresAsync()
    {
        // Đích dữ liệu: CinemaDbContext.Genres -> Domain/Entities/Genre.cs -> bảng GENRE.
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
