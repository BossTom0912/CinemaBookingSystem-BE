using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Application.Settings;
using CinemaSystem.Contracts.Showtimes;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Domain.Events;
using CinemaSystem.Infrastructure.Persistence;
using Hangfire;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

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
    private static readonly HashSet<string> ValidShowtimeStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        DomainConstants.EntityStatus.Open,
        DomainConstants.EntityStatus.Closed,
        DomainConstants.EntityStatus.Cancelled,
        DomainConstants.EntityStatus.Completed,
        DomainConstants.EntityStatus.ProcessingUnstable
    };

    private readonly CinemaDbContext _dbContext;
    private readonly IClock _clock;
    private readonly CinemaProcessingSettings _settings;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CinemaSystem.Application.Settings.SecuritySettings _securitySettings;
    private readonly CinemaSystem.Application.Settings.EmailTemplatesSettings _emailTemplates;

    public ShowtimeService(CinemaDbContext dbContext, IClock clock, IOptions<CinemaProcessingSettings> options, IOptions<CinemaSystem.Application.Settings.SecuritySettings> securityOptions, IOptions<CinemaSystem.Application.Settings.EmailTemplatesSettings> emailTemplatesOptions, IBackgroundJobClient backgroundJobClient, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _clock = clock;
        _settings = options.Value;
        _securitySettings = securityOptions.Value;
        _emailTemplates = emailTemplatesOptions.Value;
        _backgroundJobClient = backgroundJobClient;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<ServiceResult<IReadOnlyList<ShowtimeResponse>>> GetShowtimesAsync(
        CancellationToken cancellationToken)
    {
        var showtimes = await _dbContext.Showtimes
            .AsNoTracking()
            .Where(item => item.Status == BookingConstants.ShowtimeStatus.Open)
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

        var normalizedStartTime = EnsureUtc(request.StartTime);
        var minutesUntilShowtime = (normalizedStartTime - _clock.UtcNow).TotalMinutes;
        if (minutesUntilShowtime < _settings.PreShowtimeBlockingMinutes)
        {
            return ServiceResult<ShowtimeResponse>.Fail(400, $"Cannot create showtime closer than {_settings.PreShowtimeBlockingMinutes} minutes to start.", "PRE_SHOWTIME_BLOCK");
        }

        var validation = await ValidateMovieRoomAndOverlapAsync(
            request.MovieId,
            request.RoomId,
            request.StartTime,
            excludeShowtimeId: null,
            existingStartTime: null,
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
            StartTime = normalizedStartTime,
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
        bool force,
        CancellationToken cancellationToken)
    {
        var showtime = await LoadShowtimeAsync(showtimeId, tracking: true, cancellationToken);
        if (showtime is null)
        {
            return ServiceResult<ShowtimeResponse>.Fail(404, "Showtime was not found.", "SHOWTIME_NOT_FOUND");
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

        var normalizedStartTime = EnsureUtc(request.StartTime);
        var roomChanged = !string.Equals(showtime.RoomId, request.RoomId, StringComparison.Ordinal);
        var timeChanged = showtime.StartTime != normalizedStartTime;
        var coreInfoChanged = roomChanged || timeChanged;

        if (coreInfoChanged && showtime.Bookings.Any(b => b.BookingStatus == DomainConstants.EntityStatus.Paid))
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                showtime.Status = DomainConstants.EntityStatus.ProcessingUnstable;

                var paidBookings = showtime.Bookings.Where(b => b.BookingStatus == DomainConstants.EntityStatus.Paid).ToList();
                foreach (var booking in paidBookings)
                {
                    booking.BookingStatus = DomainConstants.EntityStatus.ProcessingUnstable;
                    var updateDetails = new List<string>();
                    if (roomChanged) updateDetails.Add($"Room changed to {request.RoomId}");
                    if (timeChanged) updateDetails.Add($"Start time changed to {normalizedStartTime:yyyy-MM-dd HH:mm}");
                    var updateReason = string.Join(" and ", updateDetails);

                    var customerEmail = booking.CustomerProfile?.User?.Email ?? booking.GuestEmail;
                    if (!string.IsNullOrEmpty(customerEmail))
                    {
                        var timeDiff = Math.Abs((normalizedStartTime - showtime.StartTime).TotalMinutes);
                        if (timeChanged && timeDiff >= 15)
                        {
                            var secret = _securitySettings.ConfirmationTokenSecret;
                            using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
                            var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(booking.BookingId));
                            var token = Convert.ToBase64String(hash);
                            var encodedToken = System.Uri.EscapeDataString(token);

                            string subject = _emailTemplates.ShowtimeTimeChangeSubject;
                            string message = string.Format(_emailTemplates.ShowtimeTimeChangeBody,
                                showtime.Movie.Title,
                                normalizedStartTime.ToString("dd/MM/yyyy HH:mm"),
                                booking.BookingId,
                                encodedToken);
                            _backgroundJobClient.Enqueue<IEmailService>(email => email.SendEmailAsync(customerEmail, subject, message, CancellationToken.None));
                        }
                        else
                        {
                            string subject = _emailTemplates.ShowtimeTimeChangeNoticeSubject;
                            string message = string.Format(_emailTemplates.ShowtimeTimeChangeNoticeBody,
                                showtime.Movie.Title,
                                normalizedStartTime.ToString("dd/MM/yyyy HH:mm"),
                                updateReason);
                            _backgroundJobClient.Enqueue<IEmailService>(email => email.SendEmailAsync(customerEmail, subject, message, CancellationToken.None));
                        }
                    }
                }

                // MediatR is not registered, so Hangfire cannot resolve IMediator, causing an abstract class instantiation error
                // _backgroundJobClient.Enqueue<IMediator>(m => m.Publish(new ShowtimeUnstableEvent { ShowtimeId = showtime.ShowtimeId, Reason = "Core info updated after tickets sold" }, CancellationToken.None));

                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                var unstable = await LoadShowtimeAsync(showtime.ShowtimeId, tracking: false, cancellationToken);
                return ServiceResult<ShowtimeResponse>.Ok(ToResponse(unstable!), "Showtime unstable. Manual processing required.", 200);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        var validation = await ValidateMovieRoomAndOverlapAsync(
            request.MovieId,
            request.RoomId,
            request.StartTime,
            showtime.ShowtimeId,
            showtime.StartTime,
            cancellationToken);
        if (!validation.Success)
        {
            return ServiceResult<ShowtimeResponse>.Fail(
                validation.StatusCode,
                validation.Message,
                validation.ErrorCode!);
        }

        showtime.MovieId = request.MovieId;
        showtime.RoomId = request.RoomId;
        showtime.StartTime = normalizedStartTime;
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

    public async Task<ServiceResult<ShowtimeResponse>> ChangeRoomAsync(
        string showtimeId,
        ChangeRoomRequest request,
        CancellationToken cancellationToken)
    {
        var showtime = await _dbContext.Showtimes
            .Include(s => s.ShowtimeSeats)
                .ThenInclude(sts => sts.Seat)
            .Include(s => s.Bookings)
            .FirstOrDefaultAsync(s => s.ShowtimeId == showtimeId, cancellationToken);

        if (showtime == null)
            return ServiceResult<ShowtimeResponse>.Fail(404, "Showtime not found.", "NOT_FOUND");

        var newRoom = await _dbContext.Rooms
            .Include(r => r.Seats)
            .FirstOrDefaultAsync(r => r.RoomId == request.NewRoomId, cancellationToken);

        if (newRoom == null)
            return ServiceResult<ShowtimeResponse>.Fail(404, "New room not found.", "NOT_FOUND");

        if (newRoom.RoomStatus != DomainConstants.EntityStatus.Active)
            return ServiceResult<ShowtimeResponse>.Fail(400, "New room is not active.", "ROOM_INACTIVE");

        var activeNewSeats = newRoom.Seats.Where(s => s.IsActive).ToList();

        var seatMapping = request.SeatMapping ?? new Dictionary<string, string>();

        foreach (var oldSts in showtime.ShowtimeSeats)
        {
            if (oldSts.SeatStatus == DomainConstants.EntityStatus.Booked || oldSts.SeatStatus == DomainConstants.EntityStatus.Paid || oldSts.BookingSeat != null)
            {
                string? newSeatId = null;
                if (seatMapping.TryGetValue(oldSts.SeatId, out var mappedId))
                {
                    newSeatId = mappedId;
                }
                else
                {
                    var equivalentSeat = activeNewSeats.FirstOrDefault(s => s.SeatCode == oldSts.Seat.SeatCode);
                    if (equivalentSeat != null)
                    {
                        newSeatId = equivalentSeat.SeatId;
                    }
                }

                if (newSeatId == null)
                {
                    return ServiceResult<ShowtimeResponse>.Fail(400, $"Cannot map seat {oldSts.Seat.SeatCode} to new room.", "MAPPING_FAILED");
                }
            }
        }

        _dbContext.ShowtimeSeats.RemoveRange(showtime.ShowtimeSeats);

        var newShowtimeSeats = new List<ShowtimeSeat>();
        foreach (var newSeat in activeNewSeats)
        {
            newShowtimeSeats.Add(CreateShowtimeSeat(showtime.ShowtimeId, newSeat.SeatId));
        }
        await _dbContext.ShowtimeSeats.AddRangeAsync(newShowtimeSeats, cancellationToken);

        // Cần gán lại SeatId cho các vé đã đặt (BookingSeat)
        // Note: do EF Core theo dõi, ta chỉ cần update thẳng BookingSeat.ShowtimeSeatId
        var bookingSeats = await _dbContext.BookingSeats
            .Where(bs => showtime.Bookings.Select(b => b.BookingId).Contains(bs.BookingId))
            .Include(bs => bs.ShowtimeSeat)
                .ThenInclude(sts => sts.Seat)
            .ToListAsync(cancellationToken);

        foreach (var bs in bookingSeats)
        {
            string? newSeatId = null;
            if (seatMapping.TryGetValue(bs.ShowtimeSeat.SeatId, out var mappedId))
            {
                newSeatId = mappedId;
            }
            else
            {
                var equivalentSeat = activeNewSeats.FirstOrDefault(s => s.SeatCode == bs.ShowtimeSeat.Seat.SeatCode);
                if (equivalentSeat != null) newSeatId = equivalentSeat.SeatId;
            }
            if (newSeatId != null)
            {
                var newSts = newShowtimeSeats.FirstOrDefault(sts => sts.SeatId == newSeatId);
                if (newSts != null)
                {
                    bs.ShowtimeSeatId = newSts.ShowtimeSeatId;
                    newSts.SeatStatus = DomainConstants.EntityStatus.Booked;
                }
            }
        }

        showtime.RoomId = request.NewRoomId;
        showtime.Status = DomainConstants.EntityStatus.Open;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Gửi email thông báo sơ đồ ghế mới cho khách
        var paidBookings = showtime.Bookings.Where(b => b.BookingStatus == DomainConstants.EntityStatus.Paid).ToList();
        foreach(var booking in paidBookings)
        {
            var email = booking.CustomerProfile?.User?.Email ?? booking.GuestEmail;
            if (!string.IsNullOrEmpty(email))
            {
                string subject = _emailTemplates.ShowtimeRoomChangeSubject;
                string message = string.Format(_emailTemplates.ShowtimeRoomChangeBody, newRoom.RoomName);
                _backgroundJobClient.Enqueue<IEmailService>(e => e.SendEmailAsync(email, subject, message, CancellationToken.None));
            }
        }

        var updated = await LoadShowtimeAsync(showtime.ShowtimeId, tracking: false, cancellationToken);
        return ServiceResult<ShowtimeResponse>.Ok(ToResponse(updated!), "Room changed successfully.", 200);
    }

    public async Task<ServiceResult<object>> DeleteShowtimeAsync(string showtimeId, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.Showtimes
            .Include(s => s.Bookings)
                .ThenInclude(b => b.Payments)
            .Include(s => s.Bookings)
                .ThenInclude(b => b.CustomerProfile)
                    .ThenInclude(cp => cp!.User)
            .Include(s => s.ShowtimeSeats)
            .Include(s => s.ShowtimeCancellation)
                .ThenInclude(sc => sc!.Refunds)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.ShowtimeId == showtimeId, cancellationToken);
        if (existing is null)
        {
            return ServiceResult<object>.Fail(404, "Showtime was not found.", "SHOWTIME_NOT_FOUND");
        }

        if (existing.Status == DomainConstants.EntityStatus.Completed || existing.StartTime < _clock.UtcNow)
        {
            return ServiceResult<object>.Fail(409, "Cannot cancel a showtime that has already been completed or is in the past.", "PAST_SHOWTIME");
        }

        if (existing.Bookings.Any())
        {
             await CancelShowtimeAndTriggerRefundsAsync(existing, cancellationToken);
             return ServiceResult<object>.Ok(new { showtimeId = showtimeId, deleted = true }, "Showtime softly deleted and refunds initiated.");
        }

        if (existing.ShowtimeCancellation?.Refunds.Any() == true)
        {
            return ServiceResult<object>.Fail(409, "Showtime has refund history and cannot be permanently deleted.", "RESOURCE_HAS_REFUNDS");
        }

        if (existing.ShowtimeCancellation is not null)
        {
            // SHOWTIME_CANCELLATION is an audit record containing who cancelled the
            // showtime, when it happened, and why. It must survive even when no refund
            // was generated.
            return ServiceResult<object>.Fail(
                409,
                "Showtime has cancellation history and cannot be permanently deleted.",
                "RESOURCE_HAS_CANCELLATION_HISTORY");
        }

        _dbContext.ShowtimeSeats.RemoveRange(existing.ShowtimeSeats);

        _dbContext.Showtimes.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<object>.Ok(new { showtimeId = showtimeId, deleted = true }, "Showtime permanently deleted successfully.");
    }

    private async Task<ServiceResult<ShowtimeResponse>> CancelShowtimeAndTriggerRefundsAsync(Showtime showtime, CancellationToken cancellationToken)
    {
        showtime.Status = DomainConstants.EntityStatus.Cancelled;

        foreach (var seat in showtime.ShowtimeSeats)
        {
            seat.SeatStatus = DomainConstants.EntityStatus.Available;
            seat.LockedByUserId = null;
            seat.LockedUntil = null;
        }

        var paidBookings = showtime.Bookings
            .Where(b => b.BookingStatus == DomainConstants.EntityStatus.Paid || b.BookingStatus == DomainConstants.EntityStatus.Completed)
            .ToList();

        var cancelReason = $"Showtime cancelled due to Admin update/delete.";

        var userId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userId) || userId == "string" || userId == "user")
        {
            var adminUser = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Role.RoleName == "Admin" && u.Status == DomainConstants.EntityStatus.Active, cancellationToken);

            if (adminUser != null)
            {
                userId = adminUser.UserId;
            }
            else
            {
                throw new Exception("Invalid Bearer Token or no active Admin user found for cancelling showtime.");
            }
        }

        var cancellation = showtime.ShowtimeCancellation;
        if (cancellation == null)
        {
            cancellation = new ShowtimeCancellation
            {
                ShowtimeCancellationId = NewId("STC"),
                ShowtimeId = showtime.ShowtimeId,
                CancelReason = cancelReason,
                CancelledAt = _clock.UtcNow,
                CancelledByUserId = userId,
            };
            _dbContext.ShowtimeCancellations.Add(cancellation);
        }

        foreach (var booking in paidBookings)
        {
            booking.BookingStatus = DomainConstants.EntityStatus.PendingRefund;

            var paymentId = booking.Payments.FirstOrDefault()?.PaymentId;
            var paymentProviderId = booking.Payments.FirstOrDefault()?.PaymentProviderId;

            if (string.IsNullOrEmpty(paymentId))
            {
                var dbPayment = await _dbContext.Payments
                    .FirstOrDefaultAsync(p => p.BookingId == booking.BookingId, cancellationToken);

                if (dbPayment != null)
                {
                    paymentId = dbPayment.PaymentId;
                    paymentProviderId = dbPayment.PaymentProviderId;
                }
            }
            else
            {
                bool paymentExists = await _dbContext.Payments.AnyAsync(p => p.PaymentId == paymentId, cancellationToken);
                if (!paymentExists)
                {
                    paymentId = null;
                }
            }

            if (string.IsNullOrEmpty(paymentId) || string.IsNullOrEmpty(paymentProviderId))
            {
                throw new Exception($"Cannot create refund for booking {booking.BookingId} because no valid payment or payment provider record exists in the database.");
            }

            var refund = new Refund
            {
                RefundId = NewId("REF"),
                BookingId = booking.BookingId,
                PaymentId = paymentId,
                PaymentProviderId = paymentProviderId,
                ShowtimeCancellationId = cancellation.ShowtimeCancellationId,
                RefundAmount = booking.TotalAmount,
                RefundStatus = DomainConstants.RefundStatus.Pending,
                RefundReason = cancelReason,

                RequestedAt = _clock.UtcNow
            };
            _dbContext.Refunds.Add(refund);

            var customerEmail = booking.CustomerProfile?.User?.Email ?? booking.GuestEmail;
            if (!string.IsNullOrEmpty(customerEmail))
            {
                string subject = _emailTemplates.ShowtimeCancellationSubject;
                string message = _emailTemplates.ShowtimeCancellationBody;
                _backgroundJobClient.Enqueue<IEmailService>(email => email.SendEmailAsync(customerEmail, subject, message, CancellationToken.None));
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var updated = await LoadShowtimeAsync(showtime.ShowtimeId, tracking: false, cancellationToken);
        return ServiceResult<ShowtimeResponse>.Ok(ToResponse(updated!), "Showtime cancelled softly and refunds initiated.", 200);
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
                .ThenInclude(b => b.Payments)
            .Include(item => item.Bookings)
                .ThenInclude(b => b.CustomerProfile)
                    .ThenInclude(cp => cp!.User)
            .AsSplitQuery()
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
        DateTime? existingStartTime,
        CancellationToken cancellationToken)
    {
        var movie = await _dbContext.Movies.FirstOrDefaultAsync(
            item => item.MovieId == movieId,
            cancellationToken);
        if (movie is null)
        {
            return ShowtimeValidationResult.Fail(404, "Movie was not found.", "MOVIE_NOT_FOUND");
        }

        if (movie.MovieStatus == DomainConstants.EntityStatus.Archived || movie.MovieStatus == DomainConstants.EntityStatus.Inactive)
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

        if (room.RoomStatus != DomainConstants.EntityStatus.Active || room.Cinema.CinemaStatus != DomainConstants.EntityStatus.Active)
        {
            return ShowtimeValidationResult.Fail(400, "Room or cinema is not active.", "ROOM_NOT_AVAILABLE");
        }

        var normalizedStartTime = EnsureUtc(startTime);
        if (existingStartTime == null || normalizedStartTime != EnsureUtc(existingStartTime.Value))
        {
            if (normalizedStartTime <= _clock.UtcNow)
            {
                return ShowtimeValidationResult.Fail(
                    400,
                    "Start time must be in the future.",
                    "INVALID_START_TIME");
            }
        }
        var endTime = normalizedStartTime.AddMinutes(movie.DurationMinutes + _settings.ScreeningRoomCleaningMinutes);
        var hasOverlap = await _dbContext.Showtimes.AnyAsync(
            item => item.RoomId == roomId
                && item.ShowtimeId != excludeShowtimeId
                && item.Status != DomainConstants.EntityStatus.Cancelled
                && normalizedStartTime < item.EndTime // strict inequality allows touching ends
                && endTime > item.StartTime,
            cancellationToken);

        if (hasOverlap)
        {
            return ShowtimeValidationResult.Fail(
                409,
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
            SeatStatus = DomainConstants.EntityStatus.Available
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
