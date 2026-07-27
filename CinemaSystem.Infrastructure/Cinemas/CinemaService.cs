using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Cinemas;
using CinemaSystem.Domain.Entities;
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

    public async Task<ServiceResult<CinemaResponse>> GetCinemaByIdAsync(string cinemaId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cinemaId))
        {
            return ServiceResult<CinemaResponse>.Fail(400, "Mã rạp chiếu không được để trống.", "BAD_REQUEST");
        }

        var cinema = await _dbContext.Cinemas
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CinemaId == cinemaId, cancellationToken);

        if (cinema == null)
        {
            return ServiceResult<CinemaResponse>.Fail(404, $"Không tìm thấy rạp chiếu với ID '{cinemaId}'.", "NOT_FOUND");
        }

        var response = MapToResponse(cinema);
        return ServiceResult<CinemaResponse>.Ok(response, "Lấy thông tin rạp thành công.");
    }

    public async Task<ServiceResult<CinemaResponse>> CreateCinemaAsync(CreateCinemaRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return ServiceResult<CinemaResponse>.Fail(400, "Dữ liệu tạo rạp chiếu không hợp lệ.", "BAD_REQUEST");
        }

        if (string.IsNullOrWhiteSpace(request.CinemaName))
        {
            return ServiceResult<CinemaResponse>.Fail(400, "Tên rạp chiếu không được để trống.", "BAD_REQUEST");
        }

        if (string.IsNullOrWhiteSpace(request.Address))
        {
            return ServiceResult<CinemaResponse>.Fail(400, "Địa chỉ rạp chiếu không được để trống.", "BAD_REQUEST");
        }

        if (string.IsNullOrWhiteSpace(request.City))
        {
            return ServiceResult<CinemaResponse>.Fail(400, "Thành phố không được để trống.", "BAD_REQUEST");
        }

        var newId = "CINEMA_" + Guid.NewGuid().ToString("N")[..8].ToUpper();

        var cinema = new Cinema
        {
            CinemaId = newId,
            CinemaName = request.CinemaName.Trim(),
            Address = request.Address.Trim(),
            City = request.City.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            CinemaStatus = string.IsNullOrWhiteSpace(request.CinemaStatus) ? "ACTIVE" : request.CinemaStatus.Trim().ToUpper()
        };

        _dbContext.Cinemas.Add(cinema);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _cache.Remove(CacheKey);

        var response = MapToResponse(cinema);
        return ServiceResult<CinemaResponse>.Ok(response, "Tạo rạp chiếu mới thành công.");
    }

    public async Task<ServiceResult<CinemaResponse>> UpdateCinemaAsync(string cinemaId, UpdateCinemaRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cinemaId))
        {
            return ServiceResult<CinemaResponse>.Fail(400, "Mã rạp chiếu không được để trống.", "BAD_REQUEST");
        }

        if (request == null)
        {
            return ServiceResult<CinemaResponse>.Fail(400, "Dữ liệu cập nhật không hợp lệ.", "BAD_REQUEST");
        }

        var cinema = await _dbContext.Cinemas
            .FirstOrDefaultAsync(c => c.CinemaId == cinemaId, cancellationToken);

        if (cinema == null)
        {
            return ServiceResult<CinemaResponse>.Fail(404, $"Không tìm thấy rạp chiếu với mã ID '{cinemaId}'.", "NOT_FOUND");
        }

        if (!string.IsNullOrWhiteSpace(request.CinemaName))
        {
            cinema.CinemaName = request.CinemaName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Address))
        {
            cinema.Address = request.Address.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.City))
        {
            cinema.City = request.City.Trim();
        }

        cinema.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();

        if (!string.IsNullOrWhiteSpace(request.CinemaStatus))
        {
            cinema.CinemaStatus = request.CinemaStatus.Trim().ToUpper();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _cache.Remove(CacheKey);

        var response = MapToResponse(cinema);
        return ServiceResult<CinemaResponse>.Ok(response, "Cập nhật thông tin rạp chiếu thành công.");
    }

    public async Task<ServiceResult<bool>> DeleteCinemaAsync(string cinemaId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cinemaId))
        {
            return ServiceResult<bool>.Fail(400, "Mã rạp chiếu không được để trống.", "BAD_REQUEST");
        }

        var cinema = await _dbContext.Cinemas
            .FirstOrDefaultAsync(c => c.CinemaId == cinemaId, cancellationToken);

        if (cinema == null)
        {
            return ServiceResult<bool>.Fail(404, $"Không tìm thấy rạp chiếu với mã ID '{cinemaId}'.", "NOT_FOUND");
        }

        var hasRooms = await _dbContext.Rooms.AnyAsync(r => r.CinemaId == cinemaId, cancellationToken);
        if (hasRooms)
        {
            cinema.CinemaStatus = "INACTIVE";
        }
        else
        {
            _dbContext.Cinemas.Remove(cinema);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _cache.Remove(CacheKey);

        return ServiceResult<bool>.Ok(true, "Xóa / Tạm dừng rạp chiếu thành công.");
    }

    private static CinemaResponse MapToResponse(Cinema cinema)
    {
        return new CinemaResponse
        {
            CinemaId = cinema.CinemaId,
            CinemaName = cinema.CinemaName,
            Address = cinema.Address,
            City = cinema.City,
            PhoneNumber = cinema.PhoneNumber,
            CinemaStatus = cinema.CinemaStatus
        };
    }
}
