using System.Data;
using System.Net;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Tickets;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Infrastructure.Tickets;

public sealed class TicketScanService : ITicketScanService
{
    private readonly CinemaDbContext _dbContext;
    private readonly IClock _clock;
    private readonly TicketScanSettings _settings;
    private readonly ILogger<TicketScanService> _logger;

    public TicketScanService(
        CinemaDbContext dbContext,
        IClock clock,
        IOptions<TicketScanSettings> settings,
        ILogger<TicketScanService> logger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<ServiceResult<ScanTicketResponse>> ScanAsync(
        string userId,
        string? cinemaScopeId,
        ScanTicketRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedUserId = userId?.Trim();
        var actor = string.IsNullOrWhiteSpace(normalizedUserId)
            ? null
            : await _dbContext.Users
                .AsNoTracking()
                .Where(user => user.UserId == normalizedUserId)
                .Select(user => new ScanActor(
                    user.UserId,
                    user.Status,
                    user.Role.RoleName))
                .FirstOrDefaultAsync(cancellationToken);
        if (actor is null
            || actor.Status != AuthConstants.UserStatus.Active)
        {
            return ServiceResult<ScanTicketResponse>.Fail(
                (int)HttpStatusCode.Unauthorized,
                "The authenticated scan actor was not found.",
                BookingConstants.TicketScanErrorCodes.ScanActorNotFound);
        }

        var actorRole = AuthConstants.Roles.Normalize(actor.RoleName);
        var roleMatchesScope = cinemaScopeId is null
            ? actorRole == AuthConstants.Roles.Admin
            : actorRole is AuthConstants.Roles.Staff or AuthConstants.Roles.Manager;
        if (!roleMatchesScope)
        {
            return ServiceResult<ScanTicketResponse>.Fail(
                (int)HttpStatusCode.Forbidden,
                "The authenticated role is not allowed to use this cinema scope.",
                BookingConstants.TicketScanErrorCodes.ScanActorRoleForbidden);
        }

        var actorUserId = actor.UserId;

        var normalizedQrCode = request.QrCode?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQrCode))
        {
            return ServiceResult<ScanTicketResponse>.Fail(
                (int)HttpStatusCode.BadRequest,
                "QR code is required.",
                BookingConstants.TicketScanErrorCodes.InvalidQrCode);
        }

        var normalizedRoomId = request.RoomId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedRoomId))
        {
            return ServiceResult<ScanTicketResponse>.Fail(
                (int)HttpStatusCode.BadRequest,
                "Room ID is required.",
                BookingConstants.TicketScanErrorCodes.TicketWrongRoom);
        }

        var staffProfileId = await _dbContext.StaffProfiles
            .AsNoTracking()
            .Where(profile =>
                profile.UserId == actorUserId
                && profile.EmploymentStatus == BookingConstants.ResourceStatus.Active)
            .Select(profile => profile.StaffProfileId)
            .FirstOrDefaultAsync(cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var ticket = await LoadTicketAsync(normalizedQrCode, cancellationToken);
        if (ticket is null)
        {
            return await FailAndCommitAsync(
                transaction,
                actorUserId,
                staffProfileId,
                null,
                normalizedQrCode,
                HttpStatusCode.NotFound,
                "Ticket was not found.",
                BookingConstants.TicketScanErrorCodes.TicketNotFound,
                cancellationToken);
        }

        var booking = ticket.BookingSeat.Booking;
        var showtime = booking.Showtime;
        var room = showtime.Room;

        if (cinemaScopeId is not null
            && !string.Equals(
                cinemaScopeId,
                room.CinemaId,
                StringComparison.OrdinalIgnoreCase))
        {
            return await FailAndCommitAsync(
                transaction,
                actorUserId,
                staffProfileId,
                ticket.TicketId,
                normalizedQrCode,
                HttpStatusCode.Forbidden,
                "Ticket belongs to another cinema.",
                BookingConstants.TicketScanErrorCodes.TicketWrongCinema,
                cancellationToken);
        }

        if (!string.Equals(
                normalizedRoomId,
                room.RoomId,
                StringComparison.OrdinalIgnoreCase))
        {
            return await FailAndCommitAsync(
                transaction,
                actorUserId,
                staffProfileId,
                ticket.TicketId,
                normalizedQrCode,
                HttpStatusCode.Conflict,
                "Ticket belongs to another screening room.",
                BookingConstants.TicketScanErrorCodes.TicketWrongRoom,
                cancellationToken);
        }

        var ticketStateFailure = GetTicketStateFailure(ticket.TicketStatus);
        if (ticketStateFailure is not null)
        {
            return await FailAndCommitAsync(
                transaction,
                actorUserId,
                staffProfileId,
                ticket.TicketId,
                normalizedQrCode,
                HttpStatusCode.Conflict,
                ticketStateFailure.Value.Message,
                ticketStateFailure.Value.ErrorCode,
                cancellationToken);
        }

        if (booking.BookingStatus is not BookingConstants.BookingStatus.Paid
            and not BookingConstants.BookingStatus.Completed)
        {
            return await FailAndCommitAsync(
                transaction,
                actorUserId,
                staffProfileId,
                ticket.TicketId,
                normalizedQrCode,
                HttpStatusCode.Conflict,
                "Booking is not eligible for check-in.",
                BookingConstants.TicketScanErrorCodes.BookingNotEligibleForCheckIn,
                cancellationToken);
        }

        if (showtime.Status == BookingConstants.ShowtimeStatus.Cancelled)
        {
            return await FailAndCommitAsync(
                transaction,
                actorUserId,
                staffProfileId,
                ticket.TicketId,
                normalizedQrCode,
                HttpStatusCode.Conflict,
                "The showtime was cancelled.",
                BookingConstants.TicketScanErrorCodes.ShowtimeCancelled,
                cancellationToken);
        }

        var now = _clock.UtcNow;
        var checkInOpensAt = EnsureUtc(showtime.StartTime)
            .AddMinutes(-_settings.OpenBeforeStartMinutes!.Value);
        if (now < checkInOpensAt)
        {
            return await FailAndCommitAsync(
                transaction,
                actorUserId,
                staffProfileId,
                ticket.TicketId,
                normalizedQrCode,
                HttpStatusCode.Conflict,
                "The check-in window has not opened yet.",
                BookingConstants.TicketScanErrorCodes.CheckInTooEarly,
                cancellationToken);
        }

        var checkInClosesAt = EnsureUtc(showtime.EndTime)
            .AddMinutes(_settings.CloseAfterEndMinutes!.Value);
        if (now > checkInClosesAt)
        {
            return await FailAndCommitAsync(
                transaction,
                actorUserId,
                staffProfileId,
                ticket.TicketId,
                normalizedQrCode,
                HttpStatusCode.Conflict,
                "The check-in window has closed.",
                BookingConstants.TicketScanErrorCodes.CheckInWindowClosed,
                cancellationToken);
        }

        var affectedRows = await MarkTicketCheckedInAsync(
            ticket.TicketId,
            cancellationToken);
        if (affectedRows != 1)
        {
            return await FailAndCommitAsync(
                transaction,
                actorUserId,
                staffProfileId,
                ticket.TicketId,
                normalizedQrCode,
                HttpStatusCode.Conflict,
                "Ticket was scanned concurrently or is no longer unused.",
                BookingConstants.TicketScanErrorCodes.TicketScanConflict,
                cancellationToken);
        }

        var checkInLog = CreateCheckInLog(
            actorUserId,
            staffProfileId,
            ticket.TicketId,
            normalizedQrCode,
            BookingConstants.CheckInResult.Success,
            null,
            now);
        _dbContext.CheckinLogs.Add(checkInLog);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Ticket {TicketId} checked in by user {UserId} at cinema {CinemaId}.",
            ticket.TicketId,
            actorUserId,
            room.CinemaId);

        return ServiceResult<ScanTicketResponse>.Ok(
            ToResponse(ticket, checkInLog),
            "Ticket checked in successfully.");
    }

    private async Task<Ticket?> LoadTicketAsync(
        string qrCode,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Tickets
            .AsNoTracking()
            .Include(ticket => ticket.BookingSeat)
                .ThenInclude(bookingSeat => bookingSeat.Booking)
                    .ThenInclude(booking => booking.Showtime)
                        .ThenInclude(showtime => showtime.Movie)
            .Include(ticket => ticket.BookingSeat)
                .ThenInclude(bookingSeat => bookingSeat.Booking)
                    .ThenInclude(booking => booking.Showtime)
                        .ThenInclude(showtime => showtime.Room)
                            .ThenInclude(room => room.Cinema)
            .Include(ticket => ticket.BookingSeat)
                .ThenInclude(bookingSeat => bookingSeat.ShowtimeSeat)
                    .ThenInclude(showtimeSeat => showtimeSeat.Seat)
            .FirstOrDefaultAsync(
                ticket => ticket.QrCode == qrCode,
                cancellationToken);
    }

    private async Task<int> MarkTicketCheckedInAsync(
        string ticketId,
        CancellationToken cancellationToken)
    {
        if (_dbContext.Database.IsRelational())
        {
            return await _dbContext.Tickets
                .Where(ticket =>
                    ticket.TicketId == ticketId
                    && ticket.TicketStatus == BookingConstants.TicketStatus.Unused)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        ticket => ticket.TicketStatus,
                        BookingConstants.TicketStatus.CheckedIn),
                    cancellationToken);
        }

        var ticket = await _dbContext.Tickets
            .FirstOrDefaultAsync(
                item => item.TicketId == ticketId,
                cancellationToken);
        if (ticket is null
            || ticket.TicketStatus != BookingConstants.TicketStatus.Unused)
        {
            return 0;
        }

        ticket.TicketStatus = BookingConstants.TicketStatus.CheckedIn;
        return 1;
    }

    private async Task<ServiceResult<ScanTicketResponse>> FailAndCommitAsync(
        IDbContextTransaction transaction,
        string scannedByUserId,
        string? staffProfileId,
        string? ticketId,
        string rawQrCode,
        HttpStatusCode statusCode,
        string message,
        string errorCode,
        CancellationToken cancellationToken)
    {
        _dbContext.CheckinLogs.Add(CreateCheckInLog(
            scannedByUserId,
            staffProfileId,
            ticketId,
            rawQrCode,
            BookingConstants.CheckInResult.Failed,
            errorCode,
            _clock.UtcNow));
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<ScanTicketResponse>.Fail(
            (int)statusCode,
            message,
            errorCode);
    }

    private static CheckinLog CreateCheckInLog(
        string scannedByUserId,
        string? staffProfileId,
        string? ticketId,
        string rawQrCode,
        string result,
        string? failureReason,
        DateTime scanTime)
    {
        return new CheckinLog
        {
            CheckInLogId = NewId(BookingConstants.EntityIdPrefix.CheckInLog),
            TicketId = ticketId,
            StaffProfileId = staffProfileId,
            ScannedByUserId = scannedByUserId,
            ScanTime = scanTime,
            Result = result,
            FailureReason = failureReason,
            RawQrCode = rawQrCode
        };
    }

    private static (string Message, string ErrorCode)? GetTicketStateFailure(
        string ticketStatus)
    {
        return ticketStatus switch
        {
            BookingConstants.TicketStatus.CheckedIn => (
                "Ticket has already been checked in.",
                BookingConstants.TicketScanErrorCodes.TicketAlreadyCheckedIn),
            BookingConstants.TicketStatus.Cancelled => (
                "Ticket was cancelled.",
                BookingConstants.TicketScanErrorCodes.TicketCancelled),
            BookingConstants.TicketStatus.Refunded => (
                "Ticket was refunded.",
                BookingConstants.TicketScanErrorCodes.TicketRefunded),
            BookingConstants.TicketStatus.Unused => null,
            _ => (
                "Ticket is not in a usable state.",
                BookingConstants.TicketScanErrorCodes.TicketNotUsable)
        };
    }

    private static ScanTicketResponse ToResponse(
        Ticket ticket,
        CheckinLog checkInLog)
    {
        var bookingSeat = ticket.BookingSeat;
        var booking = bookingSeat.Booking;
        var showtime = booking.Showtime;
        var room = showtime.Room;

        return new ScanTicketResponse
        {
            TicketId = ticket.TicketId,
            TicketStatus = BookingConstants.TicketStatus.CheckedIn,
            CheckInLogId = checkInLog.CheckInLogId,
            ScanTime = checkInLog.ScanTime,
            BookingId = booking.BookingId,
            CinemaId = room.CinemaId,
            CinemaName = room.Cinema.CinemaName,
            RoomId = room.RoomId,
            RoomName = room.RoomName,
            ShowtimeId = showtime.ShowtimeId,
            ShowtimeStartTime = showtime.StartTime,
            ShowtimeEndTime = showtime.EndTime,
            MovieTitle = showtime.Movie.Title,
            SeatCode = bookingSeat.ShowtimeSeat.Seat.SeatCode
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

    private static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    private sealed record ScanActor(
        string UserId,
        string Status,
        string RoleName);
}
