using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Rooms;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Infrastructure.Rooms;

public sealed class RoomService : IRoomService
{
    private const string DefaultSeatTypeId = "SEAT_TYPE_STANDARD";
    private const string DefaultSeatTypeName = "STANDARD";

    private static readonly HashSet<string> ValidRoomStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "ACTIVE",
        "INACTIVE",
        "MAINTENANCE"
    };

    private readonly CinemaDbContext _dbContext;

    public RoomService(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<IReadOnlyList<RoomResponse>>> GetRoomsAsync(CancellationToken cancellationToken)
    {
        var rooms = await _dbContext.Rooms
            .AsNoTracking()
            .Include(room => room.Cinema)
            .Include(room => room.Seats)
            .OrderBy(room => room.Cinema.CinemaName)
            .ThenBy(room => room.RoomName)
            .Select(room => ToResponse(room))
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<RoomResponse>>.Ok(rooms, "Rooms retrieved successfully.");
    }

    public async Task<ServiceResult<RoomResponse>> GetRoomByIdAsync(
        string roomId,
        CancellationToken cancellationToken)
    {
        var room = await _dbContext.Rooms
            .AsNoTracking()
            .Include(item => item.Cinema)
            .Include(item => item.Seats)
            .FirstOrDefaultAsync(item => item.RoomId == roomId, cancellationToken);

        if (room is null)
        {
            return ServiceResult<RoomResponse>.Fail(404, "Room was not found.", "ROOM_NOT_FOUND");
        }

        return ServiceResult<RoomResponse>.Ok(ToResponse(room), "Room retrieved successfully.");
    }

    public async Task<ServiceResult<RoomResponse>> CreateRoomAsync(
        string cinemaId,
        CreateRoomRequest request,
        CancellationToken cancellationToken)
    {
        var roomStatus = NormalizeStatus(request.RoomStatus);
        if (!ValidRoomStatuses.Contains(roomStatus))
        {
            return ServiceResult<RoomResponse>.Fail(400, "Room status is invalid.", "INVALID_ROOM_STATUS");
        }

        var cinema = await _dbContext.Cinemas
            .FirstOrDefaultAsync(item => item.CinemaId == cinemaId, cancellationToken);
        if (cinema is null)
        {
            return ServiceResult<RoomResponse>.Fail(404, "Cinema was not found.", "CINEMA_NOT_FOUND");
        }

        if (!string.Equals(cinema.CinemaStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<RoomResponse>.Fail(400, "Cinema is not active.", "CINEMA_NOT_ACTIVE");
        }

        var normalizedRoomName = request.RoomName.Trim();
        var duplicatedName = await _dbContext.Rooms.AnyAsync(
            item => item.CinemaId == cinemaId && item.RoomName == normalizedRoomName,
            cancellationToken);
        if (duplicatedName)
        {
            return ServiceResult<RoomResponse>.Fail(
                409,
                "Room name already exists in this cinema.",
                "DUPLICATE_ROOM_NAME");
        }

        var room = new Room
        {
            RoomId = NewId("ROOM"),
            CinemaId = cinemaId,
            RoomName = normalizedRoomName,
            Capacity = request.Capacity,
            RoomStatus = roomStatus
        };

        var seatType = await GetOrCreateDefaultSeatTypeAsync(cancellationToken);
        var seats = GenerateSeats(room.RoomId, seatType.SeatTypeId, request.Capacity, request.SeatsPerRow);

        _dbContext.Rooms.Add(room);
        await _dbContext.Seats.AddRangeAsync(seats, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        room.Cinema = cinema;
        room.Seats = seats;

        return ServiceResult<RoomResponse>.Ok(ToResponse(room), "Room created and seats generated successfully.");
    }

    public async Task<ServiceResult<RoomResponse>> UpdateRoomAsync(
        string roomId,
        UpdateRoomRequest request,
        CancellationToken cancellationToken)
    {
        var roomStatus = NormalizeStatus(request.RoomStatus);
        if (!ValidRoomStatuses.Contains(roomStatus))
        {
            return ServiceResult<RoomResponse>.Fail(400, "Room status is invalid.", "INVALID_ROOM_STATUS");
        }

        var room = await _dbContext.Rooms
            .Include(item => item.Cinema)
            .Include(item => item.Seats)
            .FirstOrDefaultAsync(item => item.RoomId == roomId, cancellationToken);
        if (room is null)
        {
            return ServiceResult<RoomResponse>.Fail(404, "Room was not found.", "ROOM_NOT_FOUND");
        }

        var normalizedRoomName = request.RoomName.Trim();
        var duplicatedName = await _dbContext.Rooms.AnyAsync(
            item => item.RoomId != roomId
                && item.CinemaId == room.CinemaId
                && item.RoomName == normalizedRoomName,
            cancellationToken);
        if (duplicatedName)
        {
            return ServiceResult<RoomResponse>.Fail(
                409,
                "Room name already exists in this cinema.",
                "DUPLICATE_ROOM_NAME");
        }

        room.RoomName = normalizedRoomName;
        room.RoomStatus = roomStatus;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<RoomResponse>.Ok(ToResponse(room), "Room updated successfully.");
    }

    public async Task<ServiceResult<object>> DeleteRoomAsync(string roomId, CancellationToken cancellationToken)
    {
        var room = await _dbContext.Rooms
            .Include(item => item.Seats)
            .Include(item => item.Showtimes)
            .ThenInclude(item => item.Bookings)
            .Include(item => item.Showtimes)
            .ThenInclude(item => item.ShowtimeSeats)
            .Include(item => item.Showtimes)
            .ThenInclude(item => item.ShowtimeCancellation)
            .ThenInclude(item => item!.Refunds)
            .FirstOrDefaultAsync(item => item.RoomId == roomId, cancellationToken);
        if (room is null)
        {
            return ServiceResult<object>.Fail(404, "Room was not found.", "ROOM_NOT_FOUND");
        }

        if (room.Showtimes.Any(item => item.Bookings.Any()))
        {
            return ServiceResult<object>.Fail(
                409,
                "Room has showtimes with bookings and cannot be permanently deleted.",
                "RESOURCE_HAS_BOOKINGS");
        }

        if (room.Showtimes.Any(item => item.ShowtimeCancellation?.Refunds.Any() == true))
        {
            return ServiceResult<object>.Fail(
                409,
                "Room has showtime refund history and cannot be permanently deleted.",
                "RESOURCE_HAS_REFUNDS");
        }

        var deletedSeats = room.Seats.Count;
        var deletedShowtimes = room.Showtimes.Count;
        var deletedShowtimeSeats = room.Showtimes.Sum(item => item.ShowtimeSeats.Count);
        var showtimeCancellations = room.Showtimes
            .Select(item => item.ShowtimeCancellation)
            .OfType<ShowtimeCancellation>()
            .ToList();

        var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            _dbContext.ShowtimeSeats.RemoveRange(room.Showtimes.SelectMany(item => item.ShowtimeSeats));
            _dbContext.ShowtimeCancellations.RemoveRange(showtimeCancellations);
            _dbContext.Showtimes.RemoveRange(room.Showtimes);
            _dbContext.Seats.RemoveRange(room.Seats);
            _dbContext.Rooms.Remove(room);

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
                roomId,
                deleted = true,
                deletedSeats,
                deletedShowtimes,
                deletedShowtimeSeats,
                deletedShowtimeCancellations = showtimeCancellations.Count
            },
            "Room permanently deleted successfully.");
    }

    private async Task<SeatType> GetOrCreateDefaultSeatTypeAsync(CancellationToken cancellationToken)
    {
        var seatType = await _dbContext.SeatTypes.FirstOrDefaultAsync(
            item => item.SeatTypeId == DefaultSeatTypeId || item.TypeName == DefaultSeatTypeName,
            cancellationToken);
        if (seatType is not null)
        {
            return seatType;
        }

        seatType = new SeatType
        {
            SeatTypeId = DefaultSeatTypeId,
            TypeName = DefaultSeatTypeName,
            ExtraFee = 0
        };
        _dbContext.SeatTypes.Add(seatType);
        return seatType;
    }

    private static List<Seat> GenerateSeats(string roomId, string seatTypeId, int capacity, int seatsPerRow)
    {
        var seats = new List<Seat>(capacity);
        for (var index = 0; index < capacity; index++)
        {
            var rowIndex = index / seatsPerRow;
            var seatNumber = (index % seatsPerRow) + 1;
            var rowLabel = ToRowLabel(rowIndex);

            seats.Add(new Seat
            {
                SeatId = NewId("SEAT"),
                RoomId = roomId,
                SeatTypeId = seatTypeId,
                RowLabel = rowLabel,
                SeatNumber = seatNumber,
                SeatCode = $"{rowLabel}{seatNumber}",
                IsActive = true
            });
        }

        return seats;
    }

    private static RoomResponse ToResponse(Room room)
    {
        return new RoomResponse
        {
            RoomId = room.RoomId,
            CinemaId = room.CinemaId,
            CinemaName = room.Cinema.CinemaName,
            RoomName = room.RoomName,
            Capacity = room.Capacity,
            RoomStatus = room.RoomStatus,
            SeatCount = room.Seats.Count
        };
    }

    private static string NormalizeStatus(string status)
    {
        return status.Trim().ToUpperInvariant();
    }

    private static string ToRowLabel(int rowIndex)
    {
        var label = string.Empty;
        var current = rowIndex;
        do
        {
            label = (char)('A' + current % 26) + label;
            current = current / 26 - 1;
        }
        while (current >= 0);

        return label;
    }

    private static string NewId(string prefix)
    {
        return $"{prefix}_{Guid.NewGuid():N}";
    }
}
