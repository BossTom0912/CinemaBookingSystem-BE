using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Rooms;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Domain.Entities;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Infrastructure.Rooms;

/// <summary>
/// Runtime room CRUD and bulk seat-generation implementation reached from
/// <c>RoomsController</c> through <see cref="IRoomService"/>.
/// </summary>
/// <remarks>
/// Validates CINEMA/ROOM state, reads and writes ROOM/SEAT with
/// <c>CinemaDbContext</c>, and returns contract DTOs through
/// <c>ServiceResult</c>. Delete is a room soft delete; it does not trigger
/// showtime cancellation or refund orchestration.
/// </remarks>
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
    public async Task<ServiceResult<RoomResponse>> CreateRoomAsync(
    string cinemaId,
    CreateRoomRequest request,
    CancellationToken cancellationToken)
    {
        var roomStatus = NormalizeStatus(request.RoomStatus);

        if (!ValidRoomStatuses.Contains(roomStatus))
        {
            return ServiceResult<RoomResponse>.Fail(
                400,
                "Room status is invalid.",
                "INVALID_ROOM_STATUS");
        }

        var cinema = await _dbContext.Cinemas
            .FirstOrDefaultAsync(
                x => x.CinemaId == cinemaId,
                cancellationToken);

        if (cinema == null)
        {
            return ServiceResult<RoomResponse>.Fail(
                404,
                "Cinema not found.",
                "CINEMA_NOT_FOUND");
        }

        // create room immediately
        var roomId = NewId("ROOM");
        var room = new Room
        {
            RoomId = roomId,
            CinemaId = cinemaId,
            RoomName = request.RoomName.Trim(),
            Capacity = request.Capacity,
            RoomStatus = roomStatus
        };

        _dbContext.Rooms.Add(room);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = ToResponse(room);
        return ServiceResult<RoomResponse>.Ok(response, "Room created successfully.", 201);
    }
    public async Task<ServiceResult<IReadOnlyList<RoomResponse>>> GetRoomsAsync(CancellationToken cancellationToken)
    {
        var rooms = await _dbContext.Rooms
            .AsNoTracking()
            .Where(room => room.RoomStatus != "INACTIVE")
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
            return ServiceResult<RoomResponse>.Fail(
                404,
                "Room was not found.",
                "ROOM_NOT_FOUND");
        }

        if (string.Equals(room.RoomStatus, "INACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<RoomResponse>.Fail(
                404,
                "Room was not found.",
                "ROOM_NOT_FOUND");
        }

        return ServiceResult<RoomResponse>.Ok(
            ToResponse(room),
            "Room retrieved successfully.");
    }
    public async Task<ServiceResult<object>> GenerateSeatsAsync(
    string roomId,
    GenerateSeatsRequest request,
    CancellationToken cancellationToken)
    {
        if (request.Rows <= 0)
        {
            return ServiceResult<object>.Fail(
                400,
                "Rows must be greater than zero.",
                "INVALID_ROWS");
        }

        if (request.Columns <= 0)
        {
            return ServiceResult<object>.Fail(
                400,
                "Columns must be greater than zero.",
                "INVALID_COLUMNS");
        }

        if (request.Rows * request.Columns > 500)
        {
            return ServiceResult<object>.Fail(
                400,
                "Total seats cannot exceed 500.",
                "CAPACITY_EXCEEDED");
        }
        var room = await _dbContext.Rooms
            .Include(x => x.Seats)
            .FirstOrDefaultAsync(
                x => x.RoomId == roomId,
                cancellationToken);

        if (room == null)
        {
            return ServiceResult<object>.Fail(
                404,
                "Room not found.",
                "ROOM_NOT_FOUND");
        }

        if (room.Seats.Any())
        {
            return ServiceResult<object>.Fail(
                409,
                "Room already has seats.",
                "ROOM_HAS_SEATS");
        }

        var seats = new List<Seat>();

        for (int row = 0; row < request.Rows; row++)
        {
            var rowLabel = ((char)('A' + row)).ToString();

            for (int col = 1; col <= request.Columns; col++)
            {
                seats.Add(new Seat
                {
                    SeatId = NewId("SEAT"),
                    RoomId = roomId,
                    SeatTypeId = request.SeatTypeId,
                    RowLabel = rowLabel,
                    SeatNumber = col,
                    SeatCode = $"{rowLabel}{col}",
                    IsActive = true
                });
            }
        }

        await _dbContext.Seats.AddRangeAsync(
            seats,
            cancellationToken);

        room.Capacity = seats.Count;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return ServiceResult<object>.Ok(
            new
            {
                RoomId = roomId,
                TotalSeats = seats.Count
            },
            "Seats generated successfully.");
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

        if (string.IsNullOrWhiteSpace(request.RoomName))
        {
            return ServiceResult<RoomResponse>.Fail(
                400,
                "Room name is required.",
                "ROOM_NAME_REQUIRED");
        }

        var normalizedRoomName = string.Join(
            " ",
            request.RoomName
                .Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));

        var duplicatedName = await _dbContext.Rooms.AnyAsync(
            item =>
                item.RoomId != roomId &&
                item.CinemaId == room.CinemaId &&
                item.RoomName.ToUpper().Trim() ==
                normalizedRoomName.ToUpper(),
            cancellationToken);

        if (duplicatedName)
        {
            return ServiceResult<RoomResponse>.Fail(
                409,
                "Room name already exists in this cinema.",
                "DUPLICATE_ROOM_NAME");
        }
        if (request.Capacity <= 0)
        {
            return ServiceResult<RoomResponse>.Fail(
                400,
                "Capacity must be greater than zero.",
                "INVALID_CAPACITY");
        }

        if (request.Capacity > 500)
        {
            return ServiceResult<RoomResponse>.Fail(
                400,
                "Capacity cannot exceed 500.",
                "CAPACITY_EXCEEDED");
        }
        room.Capacity = request.Capacity;
        if (room.Seats.Any()
    && request.Capacity < room.Seats.Count)
        {
            return ServiceResult<RoomResponse>.Fail(
                400,
                "Capacity cannot be less than existing seat count.",
                "INVALID_CAPACITY");
        }

        // apply update immediately
        room.RoomName = normalizedRoomName;
        room.Capacity = request.Capacity;
        room.RoomStatus = roomStatus;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = ToResponse(room);
        return ServiceResult<RoomResponse>.Ok(response, "Room updated successfully.", 200);
    }

    public async Task<ServiceResult<object>> DeleteRoomAsync(
    string roomId,
    CancellationToken cancellationToken)
    {
        var room = await _dbContext.Rooms
            .FirstOrDefaultAsync(item => item.RoomId == roomId, cancellationToken);

        if (room is null)
        {
            return ServiceResult<object>.Fail(
                404,
                "Room was not found.",
                "ROOM_NOT_FOUND");
        }

        // delete (soft-delete) immediately
        room.RoomStatus = "INACTIVE";
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<object>.Ok(new { RoomId = roomId, RoomStatus = room.RoomStatus }, "Room deactivated successfully.");
    }

    // Apply* methods removed — direct CRUD used





    private static RoomResponse ToResponse(Room room)
    {
        return new RoomResponse
        {
            RoomId = room.RoomId,
            CinemaId = room.CinemaId,
            CinemaName = room.Cinema?.CinemaName ?? string.Empty,
            RoomName = room.RoomName,
            Capacity = room.Capacity,
            RoomStatus = room.RoomStatus,
            SeatCount = room.Seats?.Count ?? 0
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
