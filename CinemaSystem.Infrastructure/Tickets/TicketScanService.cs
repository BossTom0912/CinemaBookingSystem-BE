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
    private static class FailureReasons
    {
        public const string TicketNotFound = "Ticket Not Found";
        public const string WrongCinema = "Wrong Cinema";
        public const string WrongRoom = "Wrong Room";
        public const string TicketAlreadyUsed = "Ticket Already Used";
        public const string TicketCancelled = "Ticket Cancelled";
        public const string TicketRefunded = "Ticket Refunded";
        public const string TicketNotUsable = "Ticket Not Usable";
        public const string BookingNotEligible = "Booking Not Eligible";
        public const string ShowtimeCancelled = "Showtime Cancelled";
        public const string InvalidTime = "Invalid Time";
        public const string ConcurrentScanConflict = "Concurrent Scan Conflict";
    }

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

    public async Task<ServiceResult<ScanTicketResponse>> PreviewAsync(
        string userId,
        string claimActorRole,
        ScanTicketRequest request,
        CancellationToken cancellationToken)
    {
        var actorResult = await ResolveScanActorAsync(userId, claimActorRole, cancellationToken);
        if (!actorResult.Success || actorResult.Data is null)
        {
            return ServiceResult<ScanTicketResponse>.Fail(
                actorResult.StatusCode,
                actorResult.Message,
                actorResult.ErrorCode ?? BookingConstants.TicketScanErrorCodes.ScanActorRoleForbidden,
                actorResult.Errors);
        }

        var actor = actorResult.Data;
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

        var ticket = await LoadTicketAsync(normalizedQrCode, cancellationToken);
        if (ticket is null)
        {
            return ServiceResult<ScanTicketResponse>.Fail(
                (int)HttpStatusCode.NotFound,
                "Ticket was not found.",
                BookingConstants.TicketScanErrorCodes.TicketNotFound);
        }

        var validationFailure = ValidateTicketForScan(ticket, actor.StaffCinemaId, normalizedRoomId, _clock.UtcNow);
        if (validationFailure is not null)
        {
            return ServiceResult<ScanTicketResponse>.Fail(
                (int)validationFailure.Value.StatusCode,
                validationFailure.Value.Message,
                validationFailure.Value.ErrorCode);
        }

        return ServiceResult<ScanTicketResponse>.Ok(
            ToResponse(ticket, null, ticket.TicketStatus, _clock.UtcNow),
            "Ticket preview retrieved successfully.");
    }

    public async Task<ServiceResult<ScanTicketResponse>> ConfirmAsync(
        string userId,
        string claimActorRole,
        ConfirmTicketScanRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedTicketId = request.TicketId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTicketId))
        {
            return ServiceResult<ScanTicketResponse>.Fail(
                (int)HttpStatusCode.BadRequest,
                "Ticket ID is required.",
                BookingConstants.TicketScanErrorCodes.InvalidQrCode);
        }

        var qrCode = await _dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.TicketId == normalizedTicketId)
            .Select(ticket => ticket.QrCode)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(qrCode))
        {
            return ServiceResult<ScanTicketResponse>.Fail(
                (int)HttpStatusCode.NotFound,
                "Ticket was not found.",
                BookingConstants.TicketScanErrorCodes.TicketNotFound);
        }

        return await ScanAsync(
            userId,
            claimActorRole,
            new ScanTicketRequest
            {
                QrCode = qrCode,
                RoomId = request.RoomId
            },
            cancellationToken);
    }

    public async Task<ServiceResult<ScanTicketResponse>> ScanAsync(
        string userId,
        string claimActorRole,
        ScanTicketRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedClaimRole = AuthConstants.Roles.Normalize(claimActorRole);
        if (normalizedClaimRole is not AuthConstants.Roles.Admin
            and not AuthConstants.Roles.Manager
            and not AuthConstants.Roles.Staff)
        {
            return ServiceResult<ScanTicketResponse>.Fail(
                (int)HttpStatusCode.Forbidden,
                "The authenticated role is not allowed to scan tickets.",
                BookingConstants.TicketScanErrorCodes.ScanActorRoleForbidden);
        }

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
        if (!string.Equals(actorRole, normalizedClaimRole, StringComparison.Ordinal))
        {
            return ServiceResult<ScanTicketResponse>.Fail(
                (int)HttpStatusCode.Forbidden,
                "The authenticated role does not match the current account role.",
                BookingConstants.TicketScanErrorCodes.ScanActorRoleForbidden);
        }

        if (actorRole is not AuthConstants.Roles.Admin
            and not AuthConstants.Roles.Manager
            and not AuthConstants.Roles.Staff)
        {
            return ServiceResult<ScanTicketResponse>.Fail(
                (int)HttpStatusCode.Forbidden,
                "The authenticated account role is not allowed to scan tickets.",
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

        var staffProfile = await _dbContext.StaffProfiles
            .AsNoTracking()
            .Where(profile =>
                profile.UserId == actorUserId
                && profile.EmploymentStatus == BookingConstants.ResourceStatus.Active)
            .Select(profile => new ScanStaffProfile(
                profile.StaffProfileId,
                profile.CinemaId))
            .FirstOrDefaultAsync(cancellationToken);
        if (actorRole is AuthConstants.Roles.Manager or AuthConstants.Roles.Staff
            && staffProfile is null)
        {
            return ServiceResult<ScanTicketResponse>.Fail(
                (int)HttpStatusCode.Forbidden,
                "Active staff profile cinema scope was not found.",
                BookingConstants.TicketScanErrorCodes.ScanActorRoleForbidden);
        }

        var staffProfileId = staffProfile?.StaffProfileId;
        var staffCinemaId = actorRole == AuthConstants.Roles.Admin
            ? null
            : staffProfile!.CinemaId;

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            var ticket = await LoadTicketAsync(normalizedQrCode, cancellationToken);
            if (ticket is null)
            {
                await using var failTx = await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
                return await FailAndCommitAsync(
                    failTx,
                    actorUserId,
                    staffProfileId,
                    null,
                    normalizedQrCode,
                    HttpStatusCode.NotFound,
                    "Ticket was not found.",
                    BookingConstants.TicketScanErrorCodes.TicketNotFound,
                    FailureReasons.TicketNotFound,
                    cancellationToken);
            }

            var booking = ticket.BookingSeat.Booking;
            var showtime = booking.Showtime;
            var room = showtime.Room;

            if (staffCinemaId is not null
                && !string.Equals(
                    staffCinemaId,
                    room.CinemaId,
                    StringComparison.OrdinalIgnoreCase))
            {
                await using var failTx = await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
                return await FailAndCommitAsync(
                    failTx,
                    actorUserId,
                    staffProfileId,
                    ticket.TicketId,
                    normalizedQrCode,
                    HttpStatusCode.Forbidden,
                    "Ticket belongs to another cinema.",
                    BookingConstants.TicketScanErrorCodes.TicketWrongCinema,
                    FailureReasons.WrongCinema,
                    cancellationToken);
            }

            if (!string.Equals(
                    normalizedRoomId,
                    room.RoomId,
                    StringComparison.OrdinalIgnoreCase))
            {
                await using var failTx = await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
                return await FailAndCommitAsync(
                    failTx,
                    actorUserId,
                    staffProfileId,
                    ticket.TicketId,
                    normalizedQrCode,
                    HttpStatusCode.Conflict,
                    "Ticket belongs to another screening room.",
                    BookingConstants.TicketScanErrorCodes.TicketWrongRoom,
                    FailureReasons.WrongRoom,
                    cancellationToken);
            }

            var ticketStateFailure = GetTicketStateFailure(ticket.TicketStatus);
            if (ticketStateFailure is not null)
            {
                await using var failTx = await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
                return await FailAndCommitAsync(
                    failTx,
                    actorUserId,
                    staffProfileId,
                    ticket.TicketId,
                    normalizedQrCode,
                    HttpStatusCode.Conflict,
                    ticketStateFailure.Value.Message,
                    ticketStateFailure.Value.ErrorCode,
                    ticketStateFailure.Value.FailureReason,
                    cancellationToken);
            }

            if (booking.BookingStatus is not BookingConstants.BookingStatus.Paid
                and not BookingConstants.BookingStatus.Completed)
            {
                await using var failTx = await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
                return await FailAndCommitAsync(
                    failTx,
                    actorUserId,
                    staffProfileId,
                    ticket.TicketId,
                    normalizedQrCode,
                    HttpStatusCode.Conflict,
                    "Booking is not eligible for check-in.",
                    BookingConstants.TicketScanErrorCodes.BookingNotEligibleForCheckIn,
                    FailureReasons.BookingNotEligible,
                    cancellationToken);
            }

            if (!string.Equals(room.RoomStatus, BookingConstants.ResourceStatus.Active, StringComparison.OrdinalIgnoreCase))
            {
                await using var failTx = await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
                return await FailAndCommitAsync(
                    failTx,
                    actorUserId,
                    staffProfileId,
                    ticket.TicketId,
                    normalizedQrCode,
                    HttpStatusCode.Conflict,
                    "The screening room is currently unavailable or under maintenance.",
                    BookingConstants.TicketScanErrorCodes.TicketNotUsable,
                    FailureReasons.TicketNotUsable,
                    cancellationToken);
            }

            if (showtime.Status == BookingConstants.ShowtimeStatus.Cancelled )
            {
                await using var failTx = await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
                return await FailAndCommitAsync(
                    failTx,
                    actorUserId,
                    staffProfileId,
                    ticket.TicketId,
                    normalizedQrCode,
                    HttpStatusCode.Conflict,
                    "The showtime was cancelled or suspended.",
                    BookingConstants.TicketScanErrorCodes.ShowtimeCancelled,
                    FailureReasons.ShowtimeCancelled,
                    cancellationToken);
            }

            var now = _clock.UtcNow;
            var checkInOpensAt = EnsureUtc(showtime.StartTime)
                .AddMinutes(-_settings.OpenBeforeStartMinutes!.Value);
            if (now < checkInOpensAt)
            {
                await using var failTx = await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
                return await FailAndCommitAsync(
                    failTx,
                    actorUserId,
                    staffProfileId,
                    ticket.TicketId,
                    normalizedQrCode,
                    HttpStatusCode.Conflict,
                    "The check-in window has not opened yet.",
                    BookingConstants.TicketScanErrorCodes.CheckInTooEarly,
                    FailureReasons.InvalidTime,
                    cancellationToken);
            }

            if (now >= EnsureUtc(showtime.EndTime))
            {
                await using var failTx = await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
                return await FailAndCommitAsync(
                    failTx,
                    actorUserId,
                    staffProfileId,
                    ticket.TicketId,
                    normalizedQrCode,
                    HttpStatusCode.Conflict,
                    "The showtime has ended; ticket check-in is no longer available.",
                    BookingConstants.TicketScanErrorCodes.CheckInWindowClosed,
                    FailureReasons.InvalidTime,
                    cancellationToken);
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

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
                    FailureReasons.ConcurrentScanConflict,
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
                ToResponse(ticket, checkInLog, BookingConstants.TicketStatus.CheckedIn, checkInLog.ScanTime),
                "Ticket checked in successfully.");
        });
    }

    private async Task<ServiceResult<ScanActorContext>> ResolveScanActorAsync(
        string userId,
        string claimActorRole,
        CancellationToken cancellationToken)
    {
        var normalizedClaimRole = AuthConstants.Roles.Normalize(claimActorRole);
        if (normalizedClaimRole is not AuthConstants.Roles.Admin
            and not AuthConstants.Roles.Manager
            and not AuthConstants.Roles.Staff)
        {
            return ServiceResult<ScanActorContext>.Fail(
                (int)HttpStatusCode.Forbidden,
                "The authenticated role is not allowed to scan tickets.",
                BookingConstants.TicketScanErrorCodes.ScanActorRoleForbidden);
        }

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
            return ServiceResult<ScanActorContext>.Fail(
                (int)HttpStatusCode.Unauthorized,
                "The authenticated scan actor was not found.",
                BookingConstants.TicketScanErrorCodes.ScanActorNotFound);
        }

        var actorRole = AuthConstants.Roles.Normalize(actor.RoleName);
        if (!string.Equals(actorRole, normalizedClaimRole, StringComparison.Ordinal))
        {
            return ServiceResult<ScanActorContext>.Fail(
                (int)HttpStatusCode.Forbidden,
                "The authenticated role does not match the current account role.",
                BookingConstants.TicketScanErrorCodes.ScanActorRoleForbidden);
        }

        if (actorRole is not AuthConstants.Roles.Admin
            and not AuthConstants.Roles.Manager
            and not AuthConstants.Roles.Staff)
        {
            return ServiceResult<ScanActorContext>.Fail(
                (int)HttpStatusCode.Forbidden,
                "The authenticated account role is not allowed to scan tickets.",
                BookingConstants.TicketScanErrorCodes.ScanActorRoleForbidden);
        }

        var staffProfile = await _dbContext.StaffProfiles
            .AsNoTracking()
            .Where(profile =>
                profile.UserId == actor.UserId
                && profile.EmploymentStatus == BookingConstants.ResourceStatus.Active)
            .Select(profile => new ScanStaffProfile(
                profile.StaffProfileId,
                profile.CinemaId))
            .FirstOrDefaultAsync(cancellationToken);

        if (actorRole is AuthConstants.Roles.Manager or AuthConstants.Roles.Staff
            && staffProfile is null)
        {
            return ServiceResult<ScanActorContext>.Fail(
                (int)HttpStatusCode.Forbidden,
                "Active staff profile cinema scope was not found.",
                BookingConstants.TicketScanErrorCodes.ScanActorRoleForbidden);
        }

        return ServiceResult<ScanActorContext>.Ok(
            new ScanActorContext(
                actor.UserId,
                actorRole,
                staffProfile?.StaffProfileId,
                actorRole == AuthConstants.Roles.Admin ? null : staffProfile!.CinemaId));
    }

    private (HttpStatusCode StatusCode, string Message, string ErrorCode, string FailureReason)? ValidateTicketForScan(
        Ticket ticket,
        string? staffCinemaId,
        string normalizedRoomId,
        DateTime now)
    {
        var booking = ticket.BookingSeat.Booking;
        var showtime = booking.Showtime;
        var room = showtime!.Room;

        if (staffCinemaId is not null
            && !string.Equals(
                staffCinemaId,
                room.CinemaId,
                StringComparison.OrdinalIgnoreCase))
        {
            return (
                HttpStatusCode.Forbidden,
                "Ticket belongs to another cinema.",
                BookingConstants.TicketScanErrorCodes.TicketWrongCinema,
                FailureReasons.WrongCinema);
        }

        if (!string.Equals(
                normalizedRoomId,
                room.RoomId,
                StringComparison.OrdinalIgnoreCase))
        {
            return (
                HttpStatusCode.Conflict,
                "Ticket belongs to another screening room.",
                BookingConstants.TicketScanErrorCodes.TicketWrongRoom,
                FailureReasons.WrongRoom);
        }

        var ticketStateFailure = GetTicketStateFailure(ticket.TicketStatus);
        if (ticketStateFailure is not null)
        {
            return (
                HttpStatusCode.Conflict,
                ticketStateFailure.Value.Message,
                ticketStateFailure.Value.ErrorCode,
                ticketStateFailure.Value.FailureReason);
        }

        if (booking.BookingStatus is not BookingConstants.BookingStatus.Paid
            and not BookingConstants.BookingStatus.Completed)
        {
            return (
                HttpStatusCode.Conflict,
                "Booking is not eligible for check-in.",
                BookingConstants.TicketScanErrorCodes.BookingNotEligibleForCheckIn,
                FailureReasons.BookingNotEligible);
        }

        if (!string.Equals(room.RoomStatus, BookingConstants.ResourceStatus.Active, StringComparison.OrdinalIgnoreCase))
        {
            return (
                HttpStatusCode.Conflict,
                "The screening room is currently unavailable or under maintenance.",
                BookingConstants.TicketScanErrorCodes.TicketNotUsable,
                FailureReasons.TicketNotUsable);
        }

        if (showtime.Status == BookingConstants.ShowtimeStatus.Cancelled)
        {
            return (
                HttpStatusCode.Conflict,
                "The showtime was cancelled or suspended.",
                BookingConstants.TicketScanErrorCodes.ShowtimeCancelled,
                FailureReasons.ShowtimeCancelled);
        }

        var checkInOpensAt = EnsureUtc(showtime.StartTime)
            .AddMinutes(-_settings.OpenBeforeStartMinutes!.Value);
        if (now < checkInOpensAt)
        {
            return (
                HttpStatusCode.Conflict,
                "The check-in window has not opened yet.",
                BookingConstants.TicketScanErrorCodes.CheckInTooEarly,
                FailureReasons.InvalidTime);
        }

        if (now >= EnsureUtc(showtime.EndTime))
        {
            return (
                HttpStatusCode.Conflict,
                "The showtime has ended; ticket check-in is no longer available.",
                BookingConstants.TicketScanErrorCodes.CheckInWindowClosed,
                FailureReasons.InvalidTime);
        }

        return null;
    }

    private async Task<Ticket?> LoadTicketAsync(
        string qrCode,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Tickets
            .AsSplitQuery()
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
            .Include(ticket => ticket.BookingSeat)
                .ThenInclude(bookingSeat => bookingSeat.Booking)
                    .ThenInclude(booking => booking.CustomerProfile)
                        .ThenInclude(customerProfile => customerProfile!.User)
            .Include(ticket => ticket.BookingSeat)
                .ThenInclude(bookingSeat => bookingSeat.Booking)
                    .ThenInclude(booking => booking.BookingSeats)
                        .ThenInclude(bookingSeat => bookingSeat.ShowtimeSeat)
                            .ThenInclude(showtimeSeat => showtimeSeat.Seat)
            .Include(ticket => ticket.BookingSeat)
                .ThenInclude(bookingSeat => bookingSeat.Booking)
                    .ThenInclude(booking => booking.BookingFbItems)
                        .ThenInclude(bookingFbItem => bookingFbItem.FbItem)
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
        string failureReason,
        CancellationToken cancellationToken)
    {
        _dbContext.CheckinLogs.Add(CreateCheckInLog(
            scannedByUserId,
            staffProfileId,
            ticketId,
            rawQrCode,
            BookingConstants.CheckInResult.Failed,
            failureReason,
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

    private static (string Message, string ErrorCode, string FailureReason)? GetTicketStateFailure(
        string ticketStatus)
    {
        return ticketStatus switch
        {
            BookingConstants.TicketStatus.CheckedIn => (
                "Ticket has already been used.",
                BookingConstants.TicketScanErrorCodes.TicketAlreadyCheckedIn,
                FailureReasons.TicketAlreadyUsed),
            BookingConstants.TicketStatus.Cancelled => (
                "Ticket was cancelled.",
                BookingConstants.TicketScanErrorCodes.TicketCancelled,
                FailureReasons.TicketCancelled),
            BookingConstants.TicketStatus.Refunded => (
                "Ticket was refunded.",
                BookingConstants.TicketScanErrorCodes.TicketRefunded,
                FailureReasons.TicketRefunded),
            BookingConstants.TicketStatus.Unused => null,
            _ => (
                "Ticket is not in a usable state.",
                BookingConstants.TicketScanErrorCodes.TicketNotUsable,
                FailureReasons.TicketNotUsable)
        };
    }

    private static ScanTicketResponse ToResponse(
        Ticket ticket,
        CheckinLog? checkInLog,
        string ticketStatus,
        DateTime scanTime)
    {
        var bookingSeat = ticket.BookingSeat;
        var booking = bookingSeat.Booking;
        var showtime = booking.Showtime!;
        var room = showtime.Room;
        var customerUser = booking.CustomerProfile?.User;
        var seatCodes = booking.BookingSeats
            .Select(item => item.ShowtimeSeat.Seat)
            .OrderBy(seat => seat.RowLabel)
            .ThenBy(seat => seat.SeatNumber)
            .Select(seat => seat.SeatCode)
            .ToArray();
        var foodAndBeverageItems = booking.BookingFbItems
            .OrderBy(item => item.FbItem.ItemName)
            .Select(item => new ScanTicketFoodAndBeverageItemResponse
            {
                FbItemId = item.FbItemId,
                ItemName = item.FbItem.ItemName,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Subtotal = item.Subtotal
            })
            .ToArray();

        return new ScanTicketResponse
        {
            TicketId = ticket.TicketId,
            TicketStatus = ticketStatus,
            CheckInLogId = checkInLog?.CheckInLogId,
            ScanTime = scanTime,
            BookingId = booking.BookingId,
            BookingCode = booking.BookingId,
            CustomerName = customerUser?.FullName ?? booking.GuestName,
            CustomerPhone = customerUser?.PhoneNumber ?? booking.GuestPhone,
            CinemaId = room.CinemaId,
            CinemaName = room.Cinema.CinemaName,
            RoomId = room.RoomId,
            RoomName = room.RoomName,
            ShowtimeId = showtime.ShowtimeId,
            ShowtimeStartTime = showtime.StartTime,
            ShowtimeEndTime = showtime.EndTime,
            MovieTitle = showtime.Movie.Title,
            SeatCode = bookingSeat.ShowtimeSeat.Seat.SeatCode,
            SeatCodes = seatCodes.Length == 0
                ? new[] { bookingSeat.ShowtimeSeat.Seat.SeatCode }
                : seatCodes,
            FoodAndBeverageItems = foodAndBeverageItems
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

    private static string NewId(string prefix) => CinemaSystem.Domain.Utilities.IdGenerator.NewId(prefix);

    private sealed record ScanActor(
        string UserId,
        string Status,
        string RoleName);

    private sealed record ScanStaffProfile(
        string StaffProfileId,
        string CinemaId);

    private sealed record ScanActorContext(
        string UserId,
        string ActorRole,
        string? StaffProfileId,
        string? StaffCinemaId);
}
