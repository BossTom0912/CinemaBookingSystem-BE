using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Showtimes;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Infrastructure.Showtimes;

/// <summary>
/// Runtime showtime query/CRUD implementation reached from
/// <c>ShowtimesController</c> and queried by <c>GeminiChatbotService</c>.
/// </summary>
/// <remarks>
/// Uses MOVIE, CINEMA, ROOM, SEAT, SHOWTIME and SHOWTIME_SEAT through
/// <c>CinemaDbContext</c>. Create/update validate availability and overlap;
/// create generates per-showtime seats. Direct delete is allowed only before
/// bookings/refunds exist and must not be confused with cancel/refund UC003.
/// </remarks>
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
            .OrderBy(item => item.StartTime)
            .Select(item => new ShowtimeResponse
            {
                ShowtimeId = item.ShowtimeId,
                MovieId = item.MovieId,
                MovieTitle = item.Movie.Title,
                RoomId = item.RoomId,
                RoomName = item.Room.RoomName,
                CinemaId = item.Room.CinemaId,
                CinemaName = item.Room.Cinema.CinemaName,
                StartTime = item.StartTime,
                EndTime = item.EndTime,
                BasePrice = item.BasePrice,
                Status = item.Status,
                ShowtimeSeatCount = item.ShowtimeSeats.Count
            })
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<ShowtimeResponse>>.Ok(
            showtimes,
            "Showtimes retrieved successfully.");
    }

    public async Task<ServiceResult<ShowtimeResponse>> GetShowtimeByIdAsync(
        string showtimeId,
        CancellationToken cancellationToken)
    {
        var showtime = await _dbContext.Showtimes
            .AsNoTracking()
            .Where(item => item.ShowtimeId == showtimeId)
            .Select(item => new ShowtimeResponse
            {
                ShowtimeId = item.ShowtimeId,
                MovieId = item.MovieId,
                MovieTitle = item.Movie.Title,
                RoomId = item.RoomId,
                RoomName = item.Room.RoomName,
                CinemaId = item.Room.CinemaId,
                CinemaName = item.Room.Cinema.CinemaName,
                StartTime = item.StartTime,
                EndTime = item.EndTime,
                BasePrice = item.BasePrice,
                Status = item.Status,
                ShowtimeSeatCount = item.ShowtimeSeats.Count
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (showtime is null)
        {
            return ServiceResult<ShowtimeResponse>.Fail(404, "Showtime was not found.", "SHOWTIME_NOT_FOUND");
        }

        return ServiceResult<ShowtimeResponse>.Ok(showtime, "Showtime retrieved successfully.");
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

        var roomActiveSeats = await _dbContext.Seats
            .Where(item => item.RoomId == request.RoomId && item.IsActive)
            .OrderBy(item => item.RowLabel)
            .ThenBy(item => item.SeatNumber)
            .ToListAsync(cancellationToken);
        if (roomActiveSeats.Count == 0)
        {
            return ServiceResult<ShowtimeResponse>.Fail(400, "Room has no active seats.", "ROOM_HAS_NO_SEATS");
        }

        // create showtime immediately
        var showtimeId = NewId("SHW");
        var showtime = new Showtime
        {
            ShowtimeId = showtimeId,
            MovieId = request.MovieId,
            RoomId = request.RoomId,
            StartTime = EnsureUtc(request.StartTime),
            EndTime = validation.EndTime,
            BasePrice = request.BasePrice,
            Status = status,
            CreatedAt = _clock.UtcNow
        };

        var activeSeatsForShowtime = await _dbContext.Seats
            .Where(item => item.RoomId == showtime.RoomId && item.IsActive)
            .ToListAsync(cancellationToken);

        var showtimeSeats = activeSeatsForShowtime.Select(seat => CreateShowtimeSeat(showtime.ShowtimeId, seat.SeatId)).ToList();

        _dbContext.Showtimes.Add(showtime);
        await _dbContext.ShowtimeSeats.AddRangeAsync(showtimeSeats, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var created = await LoadShowtimeAsync(showtime.ShowtimeId, tracking: false, cancellationToken);
        return ServiceResult<ShowtimeResponse>.Ok(ToResponse(created!), "Showtime created successfully.", 201);
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
        if (request.BasePrice <= 0)
        {
            return ServiceResult<ShowtimeResponse>.Fail(
                400,
                "Base price must be greater than zero.",
                "INVALID_BASE_PRICE");
        }
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
            var activeSeats2 = await _dbContext.Seats
                .Where(item => item.RoomId == request.RoomId && item.IsActive)
                .ToListAsync(cancellationToken);
            if (activeSeats2.Count == 0)
            {
                return ServiceResult<ShowtimeResponse>.Fail(400, "Room has no active seats.", "ROOM_HAS_NO_SEATS");
            }

            _dbContext.ShowtimeSeats.RemoveRange(showtime.ShowtimeSeats);
            await _dbContext.ShowtimeSeats.AddRangeAsync(activeSeats2.Select(seat => CreateShowtimeSeat(showtime.ShowtimeId, seat.SeatId)), cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var updated = await LoadShowtimeAsync(showtime.ShowtimeId, tracking: false, cancellationToken);
        return ServiceResult<ShowtimeResponse>.Ok(ToResponse(updated!), "Showtime updated successfully.", 200);
    }

    public async Task<ServiceResult<object>> DeleteShowtimeAsync(string showtimeId, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.Showtimes
            .Include(s => s.Bookings)
            .Include(s => s.ShowtimeSeats)
            .Include(s => s.ShowtimeCancellation)
            .ThenInclude(sc => sc!.Refunds)
            .FirstOrDefaultAsync(s => s.ShowtimeId == showtimeId, cancellationToken);
        if (existing is null)
        {
            return ServiceResult<object>.Fail(404, "Showtime was not found.", "SHOWTIME_NOT_FOUND");
        }

        if (existing.Bookings.Any())
        {
            return ServiceResult<object>.Fail(409, "Showtime has bookings and cannot be permanently deleted.", "RESOURCE_HAS_BOOKINGS");
        }

        if (existing.ShowtimeCancellation?.Refunds.Any() == true)
        {
            return ServiceResult<object>.Fail(409, "Showtime has refund history and cannot be permanently deleted.", "RESOURCE_HAS_REFUNDS");
        }

        _dbContext.ShowtimeSeats.RemoveRange(existing.ShowtimeSeats);

        if (existing.ShowtimeCancellation is not null)
        {
            _dbContext.ShowtimeCancellations.Remove(existing.ShowtimeCancellation);
        }

        _dbContext.Showtimes.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<object>.Ok(new { showtimeId = showtimeId, deleted = true }, "Showtime permanently deleted successfully.");
    }

    // ChangeRequest based apply methods removed - CRUD operates directly

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

        if (!string.Equals(
        movie.MovieStatus,
        "NOW_SHOWING",
        StringComparison.OrdinalIgnoreCase))
        {
            return ShowtimeValidationResult.Fail(
                400,
                "Movie is not available for showtimes.",
                "MOVIE_NOT_SELLABLE");
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
        if (normalizedStartTime <= _clock.UtcNow)
        {
            return ShowtimeValidationResult.Fail(
                400,
                "Start time must be in the future.",
                "INVALID_START_TIME");
        }
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
            MovieTitle = showtime.Movie?.Title ?? string.Empty,
            RoomId = showtime.RoomId,
            RoomName = showtime.Room?.RoomName ?? string.Empty,
            CinemaId = showtime.Room?.CinemaId ?? string.Empty,
            CinemaName = showtime.Room?.Cinema?.CinemaName ?? string.Empty,
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
