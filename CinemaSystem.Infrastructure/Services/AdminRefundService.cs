using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Application.Settings;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Infrastructure.Services;

public class AdminRefundService : IAdminRefundService
{
    private readonly CinemaDbContext _dbContext;
    private readonly ISeatLockStore _seatLockStore;
    private readonly CinemaProcessingSettings _settings;

    public AdminRefundService(CinemaDbContext dbContext, ISeatLockStore seatLockStore, IOptions<CinemaProcessingSettings> options)
    {
        _dbContext = dbContext;
        _seatLockStore = seatLockStore;
        _settings = options.Value;
    }

    public async Task<ServiceResult<bool>> CancelShowtimesAndRefundAsync(string[] showtimeIds, string reason, CancellationToken cancellationToken)
    {
        var showtimes = await _dbContext.Showtimes
            .Include(s => s.Bookings)
                .ThenInclude(b => b.Payments)
            .Include(s => s.ShowtimeSeats)
            .Where(s => showtimeIds.Contains(s.ShowtimeId) && s.Status != DomainConstants.EntityStatus.Cancelled)
            .ToListAsync(cancellationToken);

        if (!showtimes.Any())
        {
            return ServiceResult<bool>.Ok(true, "No active showtimes to cancel.");
        }

        var now = DateTime.UtcNow;

        foreach (var showtime in showtimes)
        {
            if (showtime.Bookings.Any())
            {
                var timeUntilShowtime = (showtime.StartTime - now).TotalMinutes;
                if (timeUntilShowtime < _settings.PreShowtimeBlockingMinutes)
                {
                    return ServiceResult<bool>.Fail(409, $"Cannot cancel showtime {showtime.ShowtimeId} as it is less than {_settings.PreShowtimeBlockingMinutes} minutes away.", "SHOWTIME_TOO_CLOSE");
                }

                // Update paid bookings to PENDING_REFUND
                var bookings = showtime.Bookings.Where(b => b.BookingStatus == DomainConstants.EntityStatus.Completed || b.BookingStatus == DomainConstants.EntityStatus.Paid).ToList();
                
                var cancellation = new ShowtimeCancellation
                {
                    ShowtimeCancellationId = "STC_" + Guid.NewGuid().ToString("N"),
                    ShowtimeId = showtime.ShowtimeId,
                    CancelReason = reason,
                    CancelledAt = now,
                    CancelledByUserId = "SYSTEM", // Placeholder
                };
                _dbContext.ShowtimeCancellations.Add(cancellation);

                foreach (var booking in bookings)
                {
                    booking.BookingStatus = DomainConstants.EntityStatus.PendingRefund;
                    
                    var existingRefund = await _dbContext.Refunds.FirstOrDefaultAsync(r => r.BookingId == booking.BookingId, cancellationToken);
                    if (existingRefund == null)
                    {
                        var refund = new Refund
                        {
                            RefundId = "REF_" + Guid.NewGuid().ToString("N"),
                            BookingId = booking.BookingId,
                            PaymentId = booking.Payments.FirstOrDefault()?.PaymentId ?? "UNKNOWN",
                            PaymentProviderId = "MANUAL",
                            RefundAmount = booking.TotalAmount,
                            RefundStatus = DomainConstants.EntityStatus.PendingRefund,
                            RefundReason = reason,
                            ShowtimeCancellationId = cancellation.ShowtimeCancellationId,
                            RequestedAt = now
                        };
                        _dbContext.Refunds.Add(refund);
                    }
                    else
                    {
                        existingRefund.RefundStatus = DomainConstants.EntityStatus.PendingRefund;
                        existingRefund.RefundReason = reason;
                    }
                }
            }

            showtime.Status = DomainConstants.EntityStatus.Cancelled;

            // Free all seat configurations
            foreach (var seat in showtime.ShowtimeSeats)
            {
                seat.SeatStatus = DomainConstants.EntityStatus.Available;
                seat.LockedUntil = null;
                seat.LockedByUserId = null;
                var lockKey = $"seat-lock:{showtime.ShowtimeId}:{seat.SeatId}";
                await _seatLockStore.ReleaseAsync(lockKey, cancellationToken);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<bool>.Ok(true, "Showtimes cancelled and refunds prepared successfully.");
    }

    public async Task<ServiceResult<PagedList<RefundDto>>> GetRefundsAsync(string status, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var query = _dbContext.Refunds
            .Include(r => r.Booking)
                .ThenInclude(b => b.CustomerProfile)
                    .ThenInclude(cp => cp.User)
            .Include(r => r.Booking)
                .ThenInclude(b => b.Showtime)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToUpperInvariant();
            query = query.Where(r => r.RefundStatus == normalizedStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var refunds = await query
            .OrderByDescending(r => r.RequestedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RefundDto
            {
                BookingId = r.BookingId,
                ShowtimeId = r.Booking.ShowtimeId,
                TotalAmount = r.RefundAmount,
                RefundReason = r.RefundReason ?? string.Empty,
                BookingStatus = r.Booking.BookingStatus,
                CustomerName = r.Booking.CustomerProfile != null ? r.Booking.CustomerProfile.User.FullName : (r.Booking.GuestName ?? "Guest"),
                CustomerEmail = r.Booking.CustomerProfile != null ? r.Booking.CustomerProfile.User.Email : r.Booking.GuestEmail,
                CustomerPhone = r.Booking.CustomerProfile != null ? r.Booking.CustomerProfile.User.PhoneNumber : r.Booking.GuestPhone
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<RefundDto>(refunds, totalCount, pageIndex, pageSize);

        return ServiceResult<PagedList<RefundDto>>.Ok(pagedList, "Refunds retrieved successfully.");
    }

    public async Task<ServiceResult<bool>> ConfirmRefundAsync(string bookingId, string adminUserId, CancellationToken cancellationToken)
    {
        var booking = await _dbContext.Bookings
            .Include(b => b.Refunds)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId, cancellationToken);

        if (booking == null) return ServiceResult<bool>.Fail(404, "Booking not found.", "NOT_FOUND");
        if (booking.BookingStatus != DomainConstants.EntityStatus.PendingRefund) return ServiceResult<bool>.Fail(400, "Booking is not pending refund.", "INVALID_STATUS");

        booking.BookingStatus = DomainConstants.EntityStatus.Refunded;

        var refund = booking.Refunds.FirstOrDefault(r => r.RefundStatus == DomainConstants.EntityStatus.PendingRefund);
        if (refund != null)
        {
            refund.RefundStatus = DomainConstants.EntityStatus.Refunded;
            refund.RefundedAt = DateTime.UtcNow;
            
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        
        return ServiceResult<bool>.Ok(true, "Refund confirmed successfully.");
    }
}
