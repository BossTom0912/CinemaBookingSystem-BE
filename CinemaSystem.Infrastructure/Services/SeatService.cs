using System.Text.Json;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Seats;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Infrastructure.Services;

/// <summary>
/// Runtime seat CRUD, seat-map and temporary-lock implementation reached from
/// <c>SeatsController</c> through <see cref="ISeatService"/>.
/// </summary>
/// <remarks>
/// Persistent seat state lives in SEAT/SHOWTIME_SEAT through
/// <c>CinemaDbContext</c>. A lock is first acquired through
/// <see cref="ISeatLockStore"/> (Redis or in-process), then recorded in
/// SHOWTIME_SEAT. On database failure the distributed/in-memory lock is
/// released before the exception is propagated.
/// </remarks>
public sealed class SeatService : ISeatService
{
    private const string ActionCreate = "CREATE";
    private const string ActionUpdate = "UPDATE";
    private const string ActionDelete = "DELETE";
    private const string StatusPending = "PENDING";
    private const string StatusApproved = "APPROVED";
    private const string StatusRejected = "REJECTED";
    private const string SeatAvailable = "AVAILABLE";
    private const string SeatLocked = "LOCKED";
    private const string SeatBooked = "BOOKED";
    private static readonly TimeSpan SeatLockTtl = TimeSpan.FromMinutes(10);

    private readonly CinemaDbContext _dbContext;
    private readonly ISeatLockStore _seatLockStore;

    public SeatService(
        CinemaDbContext dbContext,
        ISeatLockStore? seatLockStore = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _seatLockStore = seatLockStore ?? new InMemorySeatLockStore();
    }

    public async Task<ServiceResult<IEnumerable<SeatResponse>>> GetSeatsByRoomAsync(
        string roomId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return ServiceResult<IEnumerable<SeatResponse>>.Fail(
                400,
                "Room ID is required.",
                "INVALID_ROOM_ID");
        }

        var seats = await _dbContext.Seats
            .AsNoTracking()
            .Where(seat => seat.RoomId == roomId)
            .OrderBy(seat => seat.RowLabel)
            .ThenBy(seat => seat.SeatNumber)
            .Select(seat => ToSeatResponse(seat))
            .ToListAsync(cancellationToken);

        return ServiceResult<IEnumerable<SeatResponse>>.Ok(
            seats,
            $"Retrieved {seats.Count} seat(s) for room {roomId}.");
    }

    public async Task<ServiceResult<bool>> CreateSeatAsync(
        CreateSeatRequest request,
        string userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return ServiceResult<bool>.Fail(401, "User is required.", "USER_REQUIRED");
        }

        var room = await _dbContext.Rooms.FirstOrDefaultAsync(r => r.RoomId == request.RoomId, cancellationToken);
        if (room == null)
        {
            return ServiceResult<bool>.Fail(404, "Room not found.", "ROOM_NOT_FOUND");
        }

        var seatTypeExists = await _dbContext.SeatTypes
            .AnyAsync(st => st.SeatTypeId == request.SeatTypeId, cancellationToken);
        if (!seatTypeExists)
        {
            return ServiceResult<bool>.Fail(404, "Seat type not found.", "SEAT_TYPE_NOT_FOUND");
        }

        var seatCode = BuildSeatCode(request.RowLabel, request.SeatNumber);
        var seatExists = await _dbContext.Seats
            .AnyAsync(s => s.RoomId == request.RoomId && s.SeatCode == seatCode, cancellationToken);
        if (seatExists)
        {
            return ServiceResult<bool>.Fail(409, $"Seat {seatCode} already exists.", "SEAT_ALREADY_EXISTS");
        }

        var seat = new Seat
        {
            SeatId = Guid.NewGuid().ToString("N"),
            RoomId = request.RoomId,
            RowLabel = request.RowLabel,
            SeatNumber = request.SeatNumber,
            SeatCode = seatCode,
            SeatTypeId = request.SeatTypeId,
            IsActive = true
        };

        await _dbContext.Seats.AddAsync(seat, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true, "Seat created successfully.", 201);
    }

    public async Task<ServiceResult<bool>> UpdateSeatAsync(
        UpdateSeatRequest request,
        string userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return ServiceResult<bool>.Fail(401, "User is required.", "USER_REQUIRED");
        }

        var seat = await _dbContext.Seats.FirstOrDefaultAsync(s => s.SeatId == request.SeatId, cancellationToken);
        if (seat == null)
        {
            return ServiceResult<bool>.Fail(404, "Seat not found.", "SEAT_NOT_FOUND");
        }

        var newSeatCode = BuildSeatCode(request.RowLabel, request.SeatNumber);
        var duplicatedSeat = await _dbContext.Seats
            .AnyAsync(item => item.RoomId == seat.RoomId && item.SeatCode == newSeatCode && item.SeatId != seat.SeatId, cancellationToken);
        if (duplicatedSeat)
        {
            return ServiceResult<bool>.Fail(409, "Seat code already exists in this room.", "DUPLICATE_SEAT");
        }

        seat.RowLabel = request.RowLabel;
        seat.SeatNumber = request.SeatNumber;
        seat.SeatCode = newSeatCode;
        seat.SeatTypeId = request.SeatTypeId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true, "Seat updated successfully.");
    }

    public async Task<ServiceResult<bool>> DeleteSeatAsync(
        string seatId,
        string userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return ServiceResult<bool>.Fail(401, "User is required.", "USER_REQUIRED");
        }

        var seat = await _dbContext.Seats.FirstOrDefaultAsync(s => s.SeatId == seatId, cancellationToken);
        if (seat == null)
        {
            return ServiceResult<bool>.Fail(404, "Seat not found.", "SEAT_NOT_FOUND");
        }

        var hasFutureShowtime = await _dbContext.ShowtimeSeats
            .AnyAsync(item => item.SeatId == seatId && item.Showtime.Status == "OPEN" && item.Showtime.StartTime > DateTime.UtcNow, cancellationToken);
        if (hasFutureShowtime)
        {
            return ServiceResult<bool>.Fail(409, "Seat is being used by future showtimes.", "SEAT_IN_ACTIVE_SHOWTIME");
        }

        seat.IsActive = false;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true, "Seat deleted successfully.");
    }

    // Approval workflow removed - direct CRUD methods are used instead

    // pending request APIs removed

    public async Task<ServiceResult<LockSeatResponse>> LockSeatAsync(
        LockSeatRequest request,
        string userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return ServiceResult<LockSeatResponse>.Fail(401, "User is required.", "USER_REQUIRED");
        }

        var showtimeSeat = await _dbContext.ShowtimeSeats
            .Include(item => item.BookingSeat)
            .FirstOrDefaultAsync(
                item =>
                    item.ShowtimeId == request.ShowtimeId
                    && item.SeatId == request.SeatId,
                cancellationToken);
        if (showtimeSeat == null)
        {
            return ServiceResult<LockSeatResponse>.Fail(
                404,
                "Showtime seat not found.",
                "SHOWTIME_SEAT_NOT_FOUND");
        }

        var now = DateTime.UtcNow;
        if (showtimeSeat.BookingSeat != null || showtimeSeat.SeatStatus == SeatBooked)
        {
            return ServiceResult<LockSeatResponse>.Fail(
                409,
                "Seat has already been sold.",
                "SEAT_SOLD");
        }

        if (showtimeSeat.SeatStatus == SeatLocked
            && showtimeSeat.LockedUntil.HasValue
            && showtimeSeat.LockedUntil.Value > now)
        {
            return ServiceResult<LockSeatResponse>.Fail(
                409,
                "Seat is temporarily locked.",
                "SEAT_LOCKED");
        }

        var lockedUntil = now.Add(SeatLockTtl);
        var lockKey = BuildSeatLockKey(request.ShowtimeId, request.SeatId);
        var locked = await _seatLockStore.TryLockAsync(
            lockKey,
            userId,
            SeatLockTtl,
            cancellationToken);
        if (!locked)
        {
            return ServiceResult<LockSeatResponse>.Fail(
                409,
                "Seat is temporarily locked.",
                "SEAT_LOCKED");
        }

        try
        {
            showtimeSeat.SeatStatus = SeatLocked;
            showtimeSeat.LockedUntil = lockedUntil;
            showtimeSeat.LockedByUserId = userId;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _seatLockStore.ReleaseAsync(lockKey, cancellationToken);
            throw;
        }

        return ServiceResult<LockSeatResponse>.Ok(
            new LockSeatResponse
            {
                ShowtimeSeatId = showtimeSeat.ShowtimeSeatId,
                ShowtimeId = showtimeSeat.ShowtimeId,
                SeatId = showtimeSeat.SeatId,
                SeatStatus = showtimeSeat.SeatStatus,
                LockedUntil = lockedUntil
            },
            "Seat locked for 10 minutes.");
    }

    public async Task<ServiceResult<UnlockSeatResponse>> UnlockSeatAsync(
        UnlockSeatRequest request,
        string userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return ServiceResult<UnlockSeatResponse>.Fail(401, "User is required.", "USER_REQUIRED");
        }

        var showtimeSeat = await _dbContext.ShowtimeSeats
            .Include(item => item.BookingSeat)
            .FirstOrDefaultAsync(
                item =>
                    item.ShowtimeId == request.ShowtimeId
                    && item.SeatId == request.SeatId,
                cancellationToken);
        if (showtimeSeat == null)
        {
            return ServiceResult<UnlockSeatResponse>.Fail(
                404,
                "Showtime seat not found.",
                "SHOWTIME_SEAT_NOT_FOUND");
        }

        if (showtimeSeat.BookingSeat != null || showtimeSeat.SeatStatus == SeatBooked)
        {
            return ServiceResult<UnlockSeatResponse>.Fail(
                409,
                "Seat has already been sold.",
                "SEAT_SOLD");
        }

        var now = DateTime.UtcNow;
        if (showtimeSeat.SeatStatus != SeatLocked
            || !showtimeSeat.LockedUntil.HasValue
            || showtimeSeat.LockedUntil.Value <= now)
        {
            showtimeSeat.SeatStatus = SeatAvailable;
            showtimeSeat.LockedUntil = null;
            showtimeSeat.LockedByUserId = null;

            await _seatLockStore.ReleaseAsync(
                BuildSeatLockKey(showtimeSeat.ShowtimeId, showtimeSeat.SeatId),
                cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return ServiceResult<UnlockSeatResponse>.Ok(
                ToUnlockSeatResponse(showtimeSeat),
                "Seat lock has already expired.");
        }

        if (!string.Equals(showtimeSeat.LockedByUserId, userId, StringComparison.Ordinal))
        {
            return ServiceResult<UnlockSeatResponse>.Fail(
                403,
                "Seat is locked by another user.",
                "SEAT_LOCKED_BY_ANOTHER_USER");
        }

        showtimeSeat.SeatStatus = SeatAvailable;
        showtimeSeat.LockedUntil = null;
        showtimeSeat.LockedByUserId = null;

        await _seatLockStore.ReleaseAsync(
            BuildSeatLockKey(showtimeSeat.ShowtimeId, showtimeSeat.SeatId),
            cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<UnlockSeatResponse>.Ok(
            ToUnlockSeatResponse(showtimeSeat),
            "Seat lock released successfully.");
    }

    public async Task<ServiceResult<SeatMapResponse>> GetSeatMapAsync(
        string showtimeId,
        CancellationToken cancellationToken)
    {
        var showtime = await _dbContext.Showtimes
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ShowtimeId == showtimeId, cancellationToken);
        if (showtime == null)
        {
            return ServiceResult<SeatMapResponse>.Fail(
                404,
                "Showtime not found.",
                "SHOWTIME_NOT_FOUND");
        }

        await ReleaseExpiredLocksAsync(showtimeId, cancellationToken);

        var seats = await _dbContext.ShowtimeSeats
            .AsNoTracking()
            .Include(item => item.BookingSeat)
            .Include(item => item.Seat)
                .ThenInclude(item => item.SeatType)
            .Where(item => item.ShowtimeId == showtimeId)
            .OrderBy(item => item.Seat.RowLabel)
            .ThenBy(item => item.Seat.SeatNumber)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var availableSeats = new List<SeatMapItemResponse>();
        var lockedSeats = new List<SeatMapItemResponse>();
        var soldSeats = new List<SeatMapItemResponse>();

        foreach (var showtimeSeat in seats)
        {
            if (showtimeSeat.BookingSeat != null || showtimeSeat.SeatStatus == SeatBooked)
            {
                soldSeats.Add(ToSeatMapItem(showtimeSeat, SeatBooked, showtime.BasePrice));
                continue;
            }

            if (showtimeSeat.SeatStatus == SeatLocked
                && showtimeSeat.LockedUntil.HasValue
                && showtimeSeat.LockedUntil.Value > now)
            {
                lockedSeats.Add(ToSeatMapItem(showtimeSeat, SeatLocked, showtime.BasePrice));
                continue;
            }

            availableSeats.Add(ToSeatMapItem(showtimeSeat, SeatAvailable, showtime.BasePrice));
        }

        return ServiceResult<SeatMapResponse>.Ok(
            new SeatMapResponse
            {
                ShowtimeId = showtimeId,
                AvailableSeats = availableSeats,
                LockedSeats = lockedSeats,
                SoldSeats = soldSeats
            },
            "Seat map retrieved successfully.");
    }

    // helper methods for pending request workflow removed

    public async Task<ServiceResult<SeatResponse>> ApplyCreateAsync(
        string requestData,
        CancellationToken cancellationToken)
    {
        CreateSeatRequest? createRequest;
        try
        {
            createRequest = JsonSerializer.Deserialize<CreateSeatRequest>(requestData);
        }
        catch (System.Text.Json.JsonException)
        {
            return ServiceResult<SeatResponse>.Fail(
                400,
                "Invalid request data.",
                "INVALID_REQUEST_DATA");
        }

        if (createRequest == null)
        {
            return ServiceResult<SeatResponse>.Fail(
                400,
                "Invalid request data.",
                "INVALID_REQUEST_DATA");
        }

        var seatCode = BuildSeatCode(createRequest.RowLabel, createRequest.SeatNumber);
        var seatExists = await _dbContext.Seats
            .AnyAsync(
                seat => seat.RoomId == createRequest.RoomId && seat.SeatCode == seatCode,
                cancellationToken);
        if (seatExists)
        {
            return ServiceResult<SeatResponse>.Fail(
                409,
                "Seat already exists.",
                "SEAT_ALREADY_EXISTS");
        }

        var seat = new Seat
        {
            SeatId = Guid.NewGuid().ToString("N"),
            RoomId = createRequest.RoomId,
            RowLabel = createRequest.RowLabel,
            SeatNumber = createRequest.SeatNumber,
            SeatCode = seatCode,
            SeatTypeId = createRequest.SeatTypeId,
            IsActive = true
        };

        _dbContext.Seats.Add(seat);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<SeatResponse>.Ok(
            ToSeatResponse(seat),
            "Seat created successfully.");
    }

    public async Task<ServiceResult<SeatResponse>> ApplyUpdateAsync(
        string requestData,
        CancellationToken cancellationToken)
    {
        UpdateSeatRequest? updateRequest;
        try
        {
            updateRequest = JsonSerializer.Deserialize<UpdateSeatRequest>(requestData);
        }
        catch (System.Text.Json.JsonException)
        {
            return ServiceResult<SeatResponse>.Fail(
                400,
                "Invalid request data.",
                "INVALID_REQUEST_DATA");
        }

        if (updateRequest == null)
        {
            return ServiceResult<SeatResponse>.Fail(
                400,
                "Invalid request data.",
                "INVALID_REQUEST_DATA");
        }

        var seat = await _dbContext.Seats
            .FirstOrDefaultAsync(item => item.SeatId == updateRequest.SeatId, cancellationToken);
        if (seat == null)
        {
            return ServiceResult<SeatResponse>.Fail(404, "Seat not found.", "SEAT_NOT_FOUND");
        }

        var newSeatCode = BuildSeatCode(updateRequest.RowLabel, updateRequest.SeatNumber);
        var duplicatedSeat = await _dbContext.Seats
            .AnyAsync(
                item =>
                    item.RoomId == seat.RoomId
                    && item.SeatCode == newSeatCode
                    && item.SeatId != seat.SeatId,
                cancellationToken);
        if (duplicatedSeat)
        {
            return ServiceResult<SeatResponse>.Fail(
                409,
                "Seat code already exists in this room.",
                "DUPLICATE_SEAT");
        }

        seat.RowLabel = updateRequest.RowLabel;
        seat.SeatNumber = updateRequest.SeatNumber;
        seat.SeatCode = newSeatCode;
        seat.SeatTypeId = updateRequest.SeatTypeId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<SeatResponse>.Ok(
            ToSeatResponse(seat),
            "Seat updated successfully.");
    }

    public async Task<ServiceResult<SeatResponse>> ApplyDeleteAsync(
        string? seatId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(seatId))
        {
            return ServiceResult<SeatResponse>.Fail(
                400,
                "Invalid request data.",
                "INVALID_REQUEST_DATA");
        }

        var seat = await _dbContext.Seats
            .FirstOrDefaultAsync(item => item.SeatId == seatId, cancellationToken);
        if (seat == null)
        {
            return ServiceResult<SeatResponse>.Fail(404, "Seat not found.", "SEAT_NOT_FOUND");
        }

        var hasFutureShowtime = await _dbContext.ShowtimeSeats
            .AnyAsync(
                item =>
                    item.SeatId == seatId
                    && item.Showtime.Status == "OPEN"
                    && item.Showtime.StartTime > DateTime.UtcNow,
                cancellationToken);
        if (hasFutureShowtime)
        {
            return ServiceResult<SeatResponse>.Fail(
                409,
                "Seat is being used by future showtimes.",
                "SEAT_IN_ACTIVE_SHOWTIME");
        }

        seat.IsActive = false;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<SeatResponse>.Ok(
            ToSeatResponse(seat),
            "Seat deleted successfully.");
    }

    private async Task ReleaseExpiredLocksAsync(
        string showtimeId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var expiredLocks = await _dbContext.ShowtimeSeats
            .Where(
                item =>
                    item.ShowtimeId == showtimeId
                    && item.SeatStatus == SeatLocked
                    && item.LockedUntil <= now)
            .ToListAsync(cancellationToken);

        if (expiredLocks.Count == 0)
        {
            return;
        }

        foreach (var expiredLock in expiredLocks)
        {
            expiredLock.SeatStatus = SeatAvailable;
            expiredLock.LockedUntil = null;
            expiredLock.LockedByUserId = null;
            await _seatLockStore.ReleaseAsync(
                BuildSeatLockKey(expiredLock.ShowtimeId, expiredLock.SeatId),
                cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static SeatResponse ToSeatResponse(Seat seat)
    {
        return new SeatResponse
        {
            SeatId = seat.SeatId,
            RoomId = seat.RoomId,
            RowLabel = seat.RowLabel,
            SeatNumber = seat.SeatNumber,
            SeatCode = seat.SeatCode,
            SeatTypeId = seat.SeatTypeId,
            IsActive = seat.IsActive
        };
    }

    private static SeatMapItemResponse ToSeatMapItem(
        ShowtimeSeat showtimeSeat,
        string status,
        decimal basePrice)
    {
        return new SeatMapItemResponse
        {
            ShowtimeSeatId = showtimeSeat.ShowtimeSeatId,
            SeatId = showtimeSeat.SeatId,
            RowLabel = showtimeSeat.Seat.RowLabel,
            SeatNumber = showtimeSeat.Seat.SeatNumber,
            SeatCode = showtimeSeat.Seat.SeatCode,
            SeatTypeId = showtimeSeat.Seat.SeatTypeId,
            Price = basePrice + showtimeSeat.Seat.SeatType.ExtraFee,
            SeatStatus = status,
            LockedUntil = showtimeSeat.LockedUntil
        };
    }

    private static UnlockSeatResponse ToUnlockSeatResponse(ShowtimeSeat showtimeSeat)
    {
        return new UnlockSeatResponse
        {
            ShowtimeSeatId = showtimeSeat.ShowtimeSeatId,
            ShowtimeId = showtimeSeat.ShowtimeId,
            SeatId = showtimeSeat.SeatId,
            SeatStatus = showtimeSeat.SeatStatus
        };
    }

    private static string BuildSeatCode(string rowLabel, int seatNumber)
    {
        return $"{rowLabel.Trim().ToUpperInvariant()}{seatNumber}";
    }

    private static string BuildSeatLockKey(string showtimeId, string seatId)
    {
        return $"seat-lock:{showtimeId}:{seatId}";
    }

    private sealed record DeleteSeatRequestData(string SeatId);
}
