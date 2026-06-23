using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Tickets;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Infrastructure.Tickets;

public sealed class TicketScanService : ITicketScanService
{
    private const string ActiveEmploymentStatus = "ACTIVE";
    private const string SuccessResult = "SUCCESS";
    private const string FailedResult = "FAILED";

    private readonly CinemaDbContext _dbContext;
    private readonly IClock _clock;
    private readonly TicketSettings _settings;

    public TicketScanService(
        CinemaDbContext dbContext,
        IClock clock,
        IOptions<TicketSettings> settings)
    {
        _dbContext = dbContext;
        _clock = clock;
        _settings = settings.Value;
    }

    public async Task<ServiceResult<ScanTicketResponse>> ScanAsync(
        string userId,
        string? cinemaScopeId,
        ScanTicketRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return ServiceResult<ScanTicketResponse>.Fail(
                401,
                "Unauthorized.",
                BookingConstants.ErrorCodes.Unauthorized);
        }

        var qrCode = request.QrCode.Trim();
        var staffProfile = await GetActiveStaffProfileAsync(userId, cancellationToken);
        if (staffProfile is null)
        {
            return ServiceResult<ScanTicketResponse>.Fail(
                403,
                "Active staff profile is required to scan tickets.",
                BookingConstants.ErrorCodes.StaffProfileRequired);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = _clock.UtcNow;
        var ticket = await LoadTicketAsync(qrCode, cancellationToken);

        if (ticket is null)
        {
            return await FailWithLogAsync(
                transaction,
                staffProfile.StaffProfileId,
                null,
                qrCode,
                now,
                404,
                "Ticket was not found.",
                BookingConstants.ErrorCodes.TicketNotFound,
                cancellationToken);
        }

        var showtime = ticket.BookingSeat.Booking.Showtime;
        var cinema = showtime.Room.Cinema;

        if (!string.IsNullOrWhiteSpace(cinemaScopeId)
            && !string.Equals(cinema.CinemaId, cinemaScopeId, StringComparison.OrdinalIgnoreCase))
        {
            return await FailWithLogAsync(
                transaction,
                staffProfile.StaffProfileId,
                ticket.TicketId,
                qrCode,
                now,
                403,
                "Ticket does not belong to the staff cinema scope.",
                "CINEMA_SCOPE_FORBIDDEN",
                cancellationToken);
        }

        if (IsStatus(ticket.TicketStatus, BookingConstants.TicketStatus.CheckedIn))
        {
            var message = await BuildAlreadyCheckedInMessageAsync(ticket.TicketId, cancellationToken);
            return await FailWithLogAsync(
                transaction,
                staffProfile.StaffProfileId,
                ticket.TicketId,
                qrCode,
                now,
                409,
                message,
                BookingConstants.ErrorCodes.TicketAlreadyCheckedIn,
                cancellationToken);
        }

        if (IsStatus(ticket.TicketStatus, BookingConstants.TicketStatus.Cancelled))
        {
            return await FailWithLogAsync(
                transaction,
                staffProfile.StaffProfileId,
                ticket.TicketId,
                qrCode,
                now,
                409,
                "Ticket has been cancelled.",
                BookingConstants.ErrorCodes.TicketCancelled,
                cancellationToken);
        }

        if (IsStatus(ticket.TicketStatus, BookingConstants.TicketStatus.Refunded))
        {
            return await FailWithLogAsync(
                transaction,
                staffProfile.StaffProfileId,
                ticket.TicketId,
                qrCode,
                now,
                409,
                "Ticket has been refunded.",
                BookingConstants.ErrorCodes.TicketRefunded,
                cancellationToken);
        }

        if (!IsStatus(ticket.TicketStatus, BookingConstants.TicketStatus.Unused))
        {
            return await FailWithLogAsync(
                transaction,
                staffProfile.StaffProfileId,
                ticket.TicketId,
                qrCode,
                now,
                409,
                "Ticket is not available for check-in.",
                BookingConstants.ErrorCodes.TicketNotUsable,
                cancellationToken);
        }

        var booking = ticket.BookingSeat.Booking;
        if (!IsPaidBooking(booking.BookingStatus))
        {
            return await FailWithLogAsync(
                transaction,
                staffProfile.StaffProfileId,
                ticket.TicketId,
                qrCode,
                now,
                409,
                "Booking is not paid.",
                BookingConstants.ErrorCodes.BookingNotPaid,
                cancellationToken);
        }

        if (IsStatus(showtime.Status, BookingConstants.ShowtimeStatus.Cancelled))
        {
            return await FailWithLogAsync(
                transaction,
                staffProfile.StaffProfileId,
                ticket.TicketId,
                qrCode,
                now,
                409,
                "Showtime has been cancelled.",
                BookingConstants.ErrorCodes.ShowtimeCancelled,
                cancellationToken);
        }

        if (!IsWithinCheckInWindow(showtime, now))
        {
            return await FailWithLogAsync(
                transaction,
                staffProfile.StaffProfileId,
                ticket.TicketId,
                qrCode,
                now,
                409,
                "Ticket is outside the allowed check-in time window.",
                BookingConstants.ErrorCodes.CheckInTimeNotAllowed,
                cancellationToken);
        }

        var rowsUpdated = await MarkTicketCheckedInAsync(ticket.TicketId, cancellationToken);
        if (rowsUpdated == 0)
        {
            return await FailWithLogAsync(
                transaction,
                staffProfile.StaffProfileId,
                ticket.TicketId,
                qrCode,
                now,
                409,
                "Ticket status changed before check-in could be completed.",
                BookingConstants.ErrorCodes.TicketStatusChanged,
                cancellationToken);
        }

        var log = CreateCheckInLog(
            staffProfile.StaffProfileId,
            ticket.TicketId,
            qrCode,
            now,
            SuccessResult,
            null);

        _dbContext.CheckinLogs.Add(log);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<ScanTicketResponse>.Ok(
            ToResponse(ticket, log, now),
            "Ticket checked in successfully.");
    }

    private async Task<StaffProfileInfo?> GetActiveStaffProfileAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.StaffProfiles
            .AsNoTracking()
            .Where(item =>
                item.UserId == userId
                && item.EmploymentStatus == ActiveEmploymentStatus)
            .Select(item => new StaffProfileInfo(item.StaffProfileId, item.CinemaId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Ticket?> LoadTicketAsync(
        string qrCode,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Tickets
            .AsNoTracking()
            .Include(item => item.BookingSeat)
                .ThenInclude(item => item.Booking)
                    .ThenInclude(item => item.Showtime)
                        .ThenInclude(item => item.Movie)
            .Include(item => item.BookingSeat)
                .ThenInclude(item => item.Booking)
                    .ThenInclude(item => item.Showtime)
                        .ThenInclude(item => item.Room)
                            .ThenInclude(item => item.Cinema)
            .Include(item => item.BookingSeat)
                .ThenInclude(item => item.ShowtimeSeat)
                    .ThenInclude(item => item.Seat)
            .AsSplitQuery()
            .FirstOrDefaultAsync(item => item.QrCode == qrCode, cancellationToken);
    }

    private async Task<ServiceResult<ScanTicketResponse>> FailWithLogAsync(
        IDbContextTransaction transaction,
        string staffProfileId,
        string? ticketId,
        string rawQrCode,
        DateTime scanTime,
        int statusCode,
        string message,
        string errorCode,
        CancellationToken cancellationToken)
    {
        _dbContext.CheckinLogs.Add(CreateCheckInLog(
            staffProfileId,
            ticketId,
            rawQrCode,
            scanTime,
            FailedResult,
            message));

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<ScanTicketResponse>.Fail(
            statusCode,
            message,
            errorCode);
    }

    private async Task<int> MarkTicketCheckedInAsync(
        string ticketId,
        CancellationToken cancellationToken)
    {
        if (_dbContext.Database.IsRelational())
        {
            return await _dbContext.Tickets
                .Where(item =>
                    item.TicketId == ticketId
                    && item.TicketStatus == BookingConstants.TicketStatus.Unused)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        item => item.TicketStatus,
                        BookingConstants.TicketStatus.CheckedIn),
                    cancellationToken);
        }

        var ticket = await _dbContext.Tickets
            .FirstOrDefaultAsync(item => item.TicketId == ticketId, cancellationToken);
        if (ticket is null
            || !IsStatus(ticket.TicketStatus, BookingConstants.TicketStatus.Unused))
        {
            return 0;
        }

        ticket.TicketStatus = BookingConstants.TicketStatus.CheckedIn;
        return 1;
    }

    private bool IsWithinCheckInWindow(Showtime showtime, DateTime now)
    {
        var leadMinutes = Math.Max(0, _settings.CheckInLeadMinutes);
        var earliestAllowed = showtime.StartTime.AddMinutes(-leadMinutes);

        return now >= earliestAllowed && now <= showtime.EndTime;
    }

    private async Task<string> BuildAlreadyCheckedInMessageAsync(
        string ticketId,
        CancellationToken cancellationToken)
    {
        var checkedInAt = await _dbContext.CheckinLogs
            .AsNoTracking()
            .Where(item =>
                item.TicketId == ticketId
                && item.Result == SuccessResult)
            .OrderByDescending(item => item.ScanTime)
            .Select(item => (DateTime?)item.ScanTime)
            .FirstOrDefaultAsync(cancellationToken);

        return checkedInAt.HasValue
            ? $"Ticket has already been checked in at {checkedInAt.Value:O}."
            : "Ticket has already been checked in.";
    }

    private static CheckinLog CreateCheckInLog(
        string staffProfileId,
        string? ticketId,
        string rawQrCode,
        DateTime scanTime,
        string result,
        string? failureReason)
    {
        return new CheckinLog
        {
            CheckInLogId = NewId("CHK"),
            TicketId = ticketId,
            StaffProfileId = staffProfileId,
            ScanTime = scanTime,
            Result = result,
            FailureReason = failureReason,
            RawQrCode = rawQrCode
        };
    }

    private static ScanTicketResponse ToResponse(
        Ticket ticket,
        CheckinLog log,
        DateTime checkedInAt)
    {
        var bookingSeat = ticket.BookingSeat;
        var booking = bookingSeat.Booking;
        var showtime = booking.Showtime;
        var room = showtime.Room;
        var cinema = room.Cinema;
        var seat = bookingSeat.ShowtimeSeat.Seat;

        return new ScanTicketResponse
        {
            CheckInLogId = log.CheckInLogId,
            CheckedInAt = checkedInAt,
            TicketId = ticket.TicketId,
            TicketStatus = BookingConstants.TicketStatus.CheckedIn,
            BookingId = booking.BookingId,
            BookingStatus = booking.BookingStatus,
            ShowtimeId = showtime.ShowtimeId,
            ShowtimeStartTime = showtime.StartTime,
            ShowtimeEndTime = showtime.EndTime,
            MovieTitle = showtime.Movie.Title,
            CinemaId = cinema.CinemaId,
            CinemaName = cinema.CinemaName,
            RoomId = room.RoomId,
            RoomName = room.RoomName,
            SeatId = seat.SeatId,
            SeatCode = seat.SeatCode,
            RowLabel = seat.RowLabel,
            SeatNumber = seat.SeatNumber
        };
    }

    private static bool IsPaidBooking(string bookingStatus)
    {
        return IsStatus(bookingStatus, BookingConstants.BookingStatus.Paid)
            || IsStatus(bookingStatus, BookingConstants.BookingStatus.Completed);
    }

    private static bool IsStatus(string? actual, string expected)
    {
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    private sealed record StaffProfileInfo(string StaffProfileId, string CinemaId);
}
