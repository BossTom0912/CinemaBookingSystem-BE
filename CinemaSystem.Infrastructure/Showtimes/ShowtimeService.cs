using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Showtimes;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Infrastructure.Showtimes;

public sealed class ShowtimeService : IShowtimeService
{
    private const int BufferMinutes = 15;

    private static readonly HashSet<string> ValidShowtimeStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "OPEN",
        "CLOSED",
        "CANCELLED",
        "COMPLETED"
    };

    private readonly CinemaDbContext _dbContext;
    private readonly IClock _clock;

    public ShowtimeService(CinemaDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<ServiceResult<IReadOnlyList<ShowtimeResponse>>> GetShowtimesAsync(
        CancellationToken cancellationToken)
    {
        var showtimes = await _dbContext.Showtimes
            .AsNoTracking()
            .Include(item => item.Movie)
            .Include(item => item.Room)
            .ThenInclude(room => room.Cinema)
            .Include(item => item.ShowtimeSeats)
            .OrderBy(item => item.StartTime)
            .Select(item => ToResponse(item))
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<ShowtimeResponse>>.Ok(
            showtimes,
            "Showtimes retrieved successfully.");
    }

    public async Task<ServiceResult<ShowtimeResponse>> GetShowtimeByIdAsync(
        string showtimeId,
        CancellationToken cancellationToken)
    {
        var showtime = await LoadShowtimeAsync(showtimeId, tracking: false, cancellationToken);
        if (showtime is null)
        {
            return ServiceResult<ShowtimeResponse>.Fail(404, "Showtime was not found.", "SHOWTIME_NOT_FOUND");
        }

        return ServiceResult<ShowtimeResponse>.Ok(ToResponse(showtime), "Showtime retrieved successfully.");
    }

    public async Task<ServiceResult<ShowtimeResponse>> CreateShowtimeAsync(
        CreateShowtimeRequest request,
        CancellationToken cancellationToken)
    {
        var status = NormalizeStatus(request.Status);
        if (!ValidShowtimeStatuses.Contains(status))
        {
            return ServiceResult<ShowtimeResponse>.Fail(400, "Showtime status is invalid.", "INVALID_SHOWTIME_STATUS");
        }

        var validation = await ValidateMovieRoomAndOverlapAsync(
            request.MovieId,
            request.RoomId,
            request.StartTime,
            excludeShowtimeId: null,
            cancellationToken);
        if (!validation.Success)
        {
            return ServiceResult<ShowtimeResponse>.Fail(
                validation.StatusCode,
                validation.Message,
                validation.ErrorCode!);
        }

        var activeSeats = await _dbContext.Seats
            .Where(item => item.RoomId == request.RoomId && item.IsActive)
            .OrderBy(item => item.RowLabel)
            .ThenBy(item => item.SeatNumber)
            .ToListAsync(cancellationToken);
        if (activeSeats.Count == 0)
        {
            return ServiceResult<ShowtimeResponse>.Fail(400, "Room has no active seats.", "ROOM_HAS_NO_SEATS");
        }

        var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var showtime = new Showtime
            {
                ShowtimeId = NewId("SHW"),
                MovieId = request.MovieId,
                RoomId = request.RoomId,
                StartTime = EnsureUtc(request.StartTime),
                EndTime = validation.EndTime,
                BasePrice = request.BasePrice,
                Status = status,
                CreatedAt = _clock.UtcNow
            };

            var showtimeSeats = activeSeats
                .Select(seat => CreateShowtimeSeat(showtime.ShowtimeId, seat.SeatId))
                .ToList();

            _dbContext.Showtimes.Add(showtime);
            await _dbContext.ShowtimeSeats.AddRangeAsync(showtimeSeats, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            var created = await LoadShowtimeAsync(showtime.ShowtimeId, tracking: false, cancellationToken);
            return ServiceResult<ShowtimeResponse>.Ok(
                ToResponse(created!),
                "Showtime created successfully.");
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public async Task<ServiceResult<ShowtimeResponse>> UpdateShowtimeAsync(
        string showtimeId,
        UpdateShowtimeRequest request,
        CancellationToken cancellationToken)
    {
        var showtime = await LoadShowtimeAsync(showtimeId, tracking: true, cancellationToken);
        if (showtime is null)
        {
            return ServiceResult<ShowtimeResponse>.Fail(404, "Showtime was not found.", "SHOWTIME_NOT_FOUND");
        }

        if (showtime.Bookings.Any())
        {
            return ServiceResult<ShowtimeResponse>.Fail(
                409,
                "Showtime has bookings and cannot be updated directly.",
                "RESOURCE_HAS_BOOKINGS");
        }

        var status = NormalizeStatus(request.Status);
        if (!ValidShowtimeStatuses.Contains(status))
        {
            return ServiceResult<ShowtimeResponse>.Fail(400, "Showtime status is invalid.", "INVALID_SHOWTIME_STATUS");
        }

        var validation = await ValidateMovieRoomAndOverlapAsync(
            request.MovieId,
            request.RoomId,
            request.StartTime,
            showtimeId,
            cancellationToken);
        if (!validation.Success)
        {
            return ServiceResult<ShowtimeResponse>.Fail(
                validation.StatusCode,
                validation.Message,
                validation.ErrorCode!);
        }

        var roomChanged = !string.Equals(showtime.RoomId, request.RoomId, StringComparison.Ordinal);
        showtime.MovieId = request.MovieId;
        showtime.RoomId = request.RoomId;
        showtime.StartTime = EnsureUtc(request.StartTime);
        showtime.EndTime = validation.EndTime;
        showtime.BasePrice = request.BasePrice;
        showtime.Status = status;

        if (roomChanged)
        {
            var activeSeats = await _dbContext.Seats
                .Where(item => item.RoomId == request.RoomId && item.IsActive)
                .ToListAsync(cancellationToken);
            if (activeSeats.Count == 0)
            {
                return ServiceResult<ShowtimeResponse>.Fail(400, "Room has no active seats.", "ROOM_HAS_NO_SEATS");
            }

            _dbContext.ShowtimeSeats.RemoveRange(showtime.ShowtimeSeats);
            await _dbContext.ShowtimeSeats.AddRangeAsync(
                activeSeats.Select(seat => CreateShowtimeSeat(showtime.ShowtimeId, seat.SeatId)),
                cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var updated = await LoadShowtimeAsync(showtimeId, tracking: false, cancellationToken);
        return ServiceResult<ShowtimeResponse>.Ok(ToResponse(updated!), "Showtime updated successfully.");
    }

    public async Task<ServiceResult<object>> DeleteShowtimeAsync(string showtimeId, CancellationToken cancellationToken)
    {
        var showtime = await _dbContext.Showtimes
            .Include(item => item.Bookings)
            .Include(item => item.ShowtimeSeats)
            .Include(item => item.ShowtimeCancellation)
            .ThenInclude(item => item!.Refunds)
            .FirstOrDefaultAsync(item => item.ShowtimeId == showtimeId, cancellationToken);
        if (showtime is null)
        {
            return ServiceResult<object>.Fail(404, "Showtime was not found.", "SHOWTIME_NOT_FOUND");
        }

        if (showtime.Bookings.Any())
        {
            return ServiceResult<object>.Fail(
                409,
                "Showtime has bookings and cannot be permanently deleted.",
                "RESOURCE_HAS_BOOKINGS");
        }

        if (showtime.ShowtimeCancellation?.Refunds.Any() == true)
        {
            return ServiceResult<object>.Fail(
                409,
                "Showtime has refund history and cannot be permanently deleted.",
                "RESOURCE_HAS_REFUNDS");
        }

        var deletedShowtimeSeats = showtime.ShowtimeSeats.Count;
        var deletedShowtimeCancellations = showtime.ShowtimeCancellation is null ? 0 : 1;
        var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            _dbContext.ShowtimeSeats.RemoveRange(showtime.ShowtimeSeats);

            if (showtime.ShowtimeCancellation is not null)
            {
                _dbContext.ShowtimeCancellations.Remove(showtime.ShowtimeCancellation);
            }

            _dbContext.Showtimes.Remove(showtime);
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }

        return ServiceResult<object>.Ok(
            new
            {
                showtimeId,
                deleted = true,
                deletedShowtimeSeats,
                deletedShowtimeCancellations
            },
            "Showtime permanently deleted successfully.");
    }

    private async Task<Showtime?> LoadShowtimeAsync(
        string showtimeId,
        bool tracking,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Showtimes
            .Include(item => item.Movie)
            .Include(item => item.Room)
            .ThenInclude(room => room.Cinema)
            .Include(item => item.ShowtimeSeats)
            .Include(item => item.Bookings)
            .AsQueryable();

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(item => item.ShowtimeId == showtimeId, cancellationToken);
    }

    private async Task<ShowtimeValidationResult> ValidateMovieRoomAndOverlapAsync(
        string movieId,
        string roomId,
        DateTime startTime,
        string? excludeShowtimeId,
        CancellationToken cancellationToken)
    {
        var movie = await _dbContext.Movies.FirstOrDefaultAsync(
            item => item.MovieId == movieId,
            cancellationToken);
        if (movie is null)
        {
            return ShowtimeValidationResult.Fail(404, "Movie was not found.", "MOVIE_NOT_FOUND");
        }

        if (string.Equals(movie.MovieStatus, "INACTIVE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(movie.AgeRating, "C", StringComparison.OrdinalIgnoreCase))
        {
            return ShowtimeValidationResult.Fail(400, "Movie is not sellable.", "MOVIE_NOT_SELLABLE");
        }

        var room = await _dbContext.Rooms
            .Include(item => item.Cinema)
            .FirstOrDefaultAsync(item => item.RoomId == roomId, cancellationToken);
        if (room is null)
        {
            return ShowtimeValidationResult.Fail(404, "Room was not found.", "ROOM_NOT_FOUND");
        }

        if (!string.Equals(room.RoomStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(room.Cinema.CinemaStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            return ShowtimeValidationResult.Fail(400, "Room or cinema is not active.", "ROOM_NOT_AVAILABLE");
        }

        var normalizedStartTime = EnsureUtc(startTime);
        var endTime = normalizedStartTime.AddMinutes(movie.DurationMinutes + BufferMinutes);
        var hasOverlap = await _dbContext.Showtimes.AnyAsync(
            item => item.RoomId == roomId
                && item.ShowtimeId != excludeShowtimeId
                && item.Status != "CANCELLED"
                && normalizedStartTime < item.EndTime
                && endTime > item.StartTime,
            cancellationToken);

        if (hasOverlap)
        {
            return ShowtimeValidationResult.Fail(
                400,
                "Showtime overlaps with an existing showtime in the same room.",
                "SHOWTIME_OVERLAP",
                endTime);
        }

        return ShowtimeValidationResult.Ok(endTime);
    }

    private static ShowtimeResponse ToResponse(Showtime showtime)
    {
        return new ShowtimeResponse
        {
            ShowtimeId = showtime.ShowtimeId,
            MovieId = showtime.MovieId,
            MovieTitle = showtime.Movie.Title,
            RoomId = showtime.RoomId,
            RoomName = showtime.Room.RoomName,
            CinemaId = showtime.Room.CinemaId,
            CinemaName = showtime.Room.Cinema.CinemaName,
            StartTime = showtime.StartTime,
            EndTime = showtime.EndTime,
            BasePrice = showtime.BasePrice,
            Status = showtime.Status,
            ShowtimeSeatCount = showtime.ShowtimeSeats.Count
        };
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static string NormalizeStatus(string status)
    {
        return status.Trim().ToUpperInvariant();
    }

    private static string NewId(string prefix)
    {
        return $"{prefix}_{Guid.NewGuid():N}";
    }

    private ShowtimeSeat CreateShowtimeSeat(string showtimeId, string seatId)
    {
        var showtimeSeat = new ShowtimeSeat
        {
            ShowtimeSeatId = NewId("STS"),
            ShowtimeId = showtimeId,
            SeatId = seatId,
            SeatStatus = "AVAILABLE"
        };

        if (!_dbContext.Database.IsRelational())
        {
            showtimeSeat.RowVersion = new byte[8];
        }

        return showtimeSeat;
    }

    private sealed record ShowtimeValidationResult(
        bool Success,
        int StatusCode,
        string Message,
        string? ErrorCode,
        DateTime EndTime)
    {
        public static ShowtimeValidationResult Ok(DateTime endTime)
        {
            return new ShowtimeValidationResult(true, 200, string.Empty, null, endTime);
        }

        public static ShowtimeValidationResult Fail(
            int statusCode,
            string message,
            string errorCode,
            DateTime endTime = default)
        {
            return new ShowtimeValidationResult(false, statusCode, message, errorCode, endTime);
        }
    }
}
