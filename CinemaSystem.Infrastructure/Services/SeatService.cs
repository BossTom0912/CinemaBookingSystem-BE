using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Seats;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Infrastructure.Services;

public sealed class SeatService : ISeatService
{
    private readonly CinemaDbContext _dbContext;

    public SeatService(CinemaDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Retrieves all seats for a specific room.
    /// </summary>
    public async Task<ServiceResult<IEnumerable<SeatResponse>>> GetSeatsByRoomAsync(
        string roomId,
        CancellationToken cancellationToken)
    {
        try
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
                .Where(s => s.RoomId == roomId)
                .Select(s => new SeatResponse
                {
                    SeatId = s.SeatId,
                    RoomId = s.RoomId,
                    RowLabel = s.RowLabel,
                    SeatNumber = s.SeatNumber,
                    SeatCode = s.SeatCode,
                    SeatTypeId = s.SeatTypeId,
                    IsActive = s.IsActive
                })
                .ToListAsync(cancellationToken);

            return ServiceResult<IEnumerable<SeatResponse>>.Ok(
                seats,
                $"Retrieved {seats.Count} seat(s) for room {roomId}.");
        }
        catch (Exception)
        {
            return ServiceResult<IEnumerable<SeatResponse>>.Fail(
                500,
                "An error occurred while retrieving seats.",
                "SEAT_RETRIEVAL_ERROR");
        }
    }

    /// <summary>
    /// Batch updates the status of multiple seats with optimized database operations.
    /// Key Optimization: Uses AddRangeAsync and SaveChangesAsync only ONCE for all seats.
    /// This reduces database round trips from N to 1 for N seats.
    /// </summary>
    public async Task<ServiceResult<int>> BatchUpdateSeatStatusAsync(
        BatchUpdateSeatStatusRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Validate input
            if (request?.SeatIds == null || !request.SeatIds.Any())
            {
                return ServiceResult<int>.Fail(
                    400,
                    "At least one seat ID is required.",
                    "EMPTY_SEAT_LIST");
            }

            var seatIds = request.SeatIds.ToList();

            // Optimization: Single database query to fetch all seats
            var seatsToUpdate = await _dbContext.Seats
                .Where(s => seatIds.Contains(s.SeatId))
                .ToListAsync(cancellationToken);

            if (seatsToUpdate.Count == 0)
            {
                return ServiceResult<int>.Fail(
                    404,
                    "No seats found with the provided IDs.",
                    "SEATS_NOT_FOUND");
            }

            // Validate room ID consistency if provided
            if (!string.IsNullOrWhiteSpace(request.RoomId))
            {
                var invalidSeats = seatsToUpdate.Where(s => s.RoomId != request.RoomId).Count();
                if (invalidSeats > 0)
                {
                    return ServiceResult<int>.Fail(
                        400,
                        $"Found {invalidSeats} seat(s) that don't belong to room {request.RoomId}.",
                        "ROOM_MISMATCH");
                }
            }

            // ====== OPTIMIZATION: BEGIN ======
            // Stage all seat updates in memory
            foreach (var seat in seatsToUpdate)
            {
                seat.IsActive = request.IsActive;
            }

            // Optimization: Use UpdateRange for batch update (marks all as modified)
            _dbContext.Seats.UpdateRange(seatsToUpdate);

            // Optimization: SaveChangesAsync ONLY ONCE after all changes are staged
            // This results in a single database round trip instead of N
            var updatedCount = await _dbContext.SaveChangesAsync(cancellationToken);
            // ====== OPTIMIZATION: END ======

            return ServiceResult<int>.Ok(
                updatedCount,
                $"Successfully updated {updatedCount} seat(s).");
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<int>.Fail(
                409,
                "Concurrency conflict: One or more seats were modified by another operation.",
                "CONCURRENCY_ERROR");
        }
        catch (DbUpdateException)
        {
            return ServiceResult<int>.Fail(
                500,
                "An error occurred while updating seats in the database.",
                "DATABASE_UPDATE_ERROR");
        }
        catch (Exception)
        {
            return ServiceResult<int>.Fail(
                500,
                "An unexpected error occurred while updating seat statuses.",
                "SEAT_UPDATE_ERROR");
        }
    }
}
