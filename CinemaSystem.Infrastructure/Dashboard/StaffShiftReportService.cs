using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Dashboard;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Infrastructure.Dashboard;

public sealed class StaffShiftReportService : IStaffShiftReportService
{
    private const string AllStaffLabel = "All staff";
    private const string AllCinemasLabel = "All cinemas";

    private readonly CinemaDbContext _dbContext;

    public StaffShiftReportService(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApiResponse<StaffShiftReportResponse>> GetShiftReportAsync(
        UserScope scope,
        StaffShiftReportQueryRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.From.HasValue || !request.To.HasValue)
        {
            return ApiResponse<StaffShiftReportResponse>.Fail(
                "From and to are required.",
                BookingConstants.StaffShiftReportErrorCode.DateRangeRequired);
        }

        var from = EnsureUtc(request.From.Value);
        var to = EnsureUtc(request.To.Value);
        if (from >= to)
        {
            return ApiResponse<StaffShiftReportResponse>.Fail(
                "From must be earlier than to.",
                BookingConstants.StaffShiftReportErrorCode.InvalidDateRange);
        }

        var scopeResult = await ResolveReportScopeAsync(scope, request, cancellationToken);
        if (scopeResult.Error is not null)
        {
            return scopeResult.Error;
        }

        var reportScope = scopeResult.Scope!;
        var checkInTransactions = await QueryCheckInTransactionsAsync(
            reportScope,
            from,
            to,
            cancellationToken);
        var counterTransactions = await QueryCounterTransactionsAsync(
            reportScope,
            from,
            to,
            cancellationToken);
        var onlineFulfillmentTransactions = await QueryOnlineFulfillmentTransactionsAsync(
            reportScope,
            from,
            to,
            cancellationToken);

        var totalCounterRevenue = counterTransactions.Sum(item => item.Amount);
        var cashRevenue = counterTransactions
            .Where(item => IsCash(item.PaymentMethod))
            .Sum(item => item.Amount);
        var transferRevenue = counterTransactions
            .Where(item => IsTransfer(item.PaymentMethod))
            .Sum(item => item.Amount);

        var transactions = checkInTransactions
            .Concat(counterTransactions)
            .Concat(onlineFulfillmentTransactions)
            .OrderByDescending(item => item.OccurredAt)
            .ThenBy(item => item.TransactionType, StringComparer.Ordinal)
            .ToList();

        return ApiResponse<StaffShiftReportResponse>.Ok(
            new StaffShiftReportResponse
            {
                StaffProfileId = reportScope.StaffProfileId,
                StaffName = reportScope.StaffName,
                CinemaId = reportScope.CinemaId,
                CinemaName = reportScope.CinemaName,
                From = from,
                To = to,
                CheckedInTicketCount = checkInTransactions.Count,
                FulfilledFbOrderCount = counterTransactions.Count + onlineFulfillmentTransactions.Count,
                CounterFbOrderCount = counterTransactions.Count,
                OnlineFulfilledFbOrderCount = onlineFulfillmentTransactions.Count,
                TotalCounterRevenue = totalCounterRevenue,
                CashRevenue = cashRevenue,
                TransferRevenue = transferRevenue,
                UnclassifiedCounterRevenue = totalCounterRevenue - cashRevenue - transferRevenue,
                Transactions = transactions
            },
            "Staff shift report loaded successfully.");
    }

    private async Task<ScopeResolution> ResolveReportScopeAsync(
        UserScope scope,
        StaffShiftReportQueryRequest request,
        CancellationToken cancellationToken)
    {
        var role = AuthConstants.Roles.Normalize(scope.Role);
        var requestedStaffProfileId = Normalize(request.StaffProfileId);
        var requestedCinemaId = Normalize(request.CinemaId);

        if (role == AuthConstants.Roles.Staff)
        {
            var currentStaff = await GetCurrentStaffProfileAsync(scope.UserId, cancellationToken);
            if (currentStaff is null)
            {
                return ScopeResolution.Fail(
                    "Active staff profile was not found.",
                    BookingConstants.StaffShiftReportErrorCode.StaffProfileNotFound);
            }

            if (requestedCinemaId is not null
                && !EqualsIgnoreCase(requestedCinemaId, currentStaff.CinemaId))
            {
                return ScopeResolution.Fail(
                    "Staff can only view shift reports within their assigned cinema.",
                    BookingConstants.StaffShiftReportErrorCode.CinemaScopeForbidden);
            }

            if (requestedStaffProfileId is not null
                && !EqualsIgnoreCase(requestedStaffProfileId, currentStaff.StaffProfileId))
            {
                return ScopeResolution.Fail(
                    "Staff can only view their own shift report.",
                    BookingConstants.StaffShiftReportErrorCode.StaffScopeForbidden);
            }

            return ScopeResolution.Ok(new ReportScope(
                currentStaff.CinemaId,
                currentStaff.CinemaName,
                currentStaff.StaffProfileId,
                currentStaff.StaffName));
        }

        if (role == AuthConstants.Roles.Manager)
        {
            var currentManager = await GetCurrentStaffProfileAsync(scope.UserId, cancellationToken);
            if (currentManager is null)
            {
                return ScopeResolution.Fail(
                    "Active manager staff profile was not found.",
                    BookingConstants.StaffShiftReportErrorCode.StaffProfileNotFound);
            }

            if (requestedCinemaId is not null
                && !EqualsIgnoreCase(requestedCinemaId, currentManager.CinemaId))
            {
                return ScopeResolution.Fail(
                    "Manager can only view shift reports within their assigned cinema.",
                    BookingConstants.StaffShiftReportErrorCode.CinemaScopeForbidden);
            }

            if (requestedStaffProfileId is not null)
            {
                var targetStaff = await GetStaffProfileAsync(requestedStaffProfileId, cancellationToken);
                if (targetStaff is null)
                {
                    return ScopeResolution.Fail(
                        "Staff profile was not found.",
                        BookingConstants.StaffShiftReportErrorCode.StaffProfileNotFound);
                }

                if (!EqualsIgnoreCase(targetStaff.CinemaId, currentManager.CinemaId))
                {
                    return ScopeResolution.Fail(
                        "Target staff profile is outside the manager cinema scope.",
                        BookingConstants.StaffShiftReportErrorCode.StaffScopeForbidden);
                }

                return ScopeResolution.Ok(new ReportScope(
                    currentManager.CinemaId,
                    currentManager.CinemaName,
                    targetStaff.StaffProfileId,
                    targetStaff.StaffName));
            }

            return ScopeResolution.Ok(new ReportScope(
                currentManager.CinemaId,
                currentManager.CinemaName,
                null,
                AllStaffLabel));
        }

        if (role == AuthConstants.Roles.Admin)
        {
            StaffProfileProjection? targetStaff = null;
            if (requestedStaffProfileId is not null)
            {
                targetStaff = await GetStaffProfileAsync(requestedStaffProfileId, cancellationToken);
                if (targetStaff is null)
                {
                    return ScopeResolution.Fail(
                        "Staff profile was not found.",
                        BookingConstants.StaffShiftReportErrorCode.StaffProfileNotFound);
                }
            }

            if (requestedCinemaId is not null
                && targetStaff is not null
                && !EqualsIgnoreCase(requestedCinemaId, targetStaff.CinemaId))
            {
                return ScopeResolution.Fail(
                    "Target staff profile does not belong to the requested cinema.",
                    BookingConstants.StaffShiftReportErrorCode.StaffCinemaMismatch);
            }

            var cinemaId = requestedCinemaId ?? targetStaff?.CinemaId;
            var cinemaName = AllCinemasLabel;
            if (cinemaId is not null)
            {
                cinemaName = await _dbContext.Cinemas
                    .AsNoTracking()
                    .Where(cinema => cinema.CinemaId == cinemaId)
                    .Select(cinema => cinema.CinemaName)
                    .FirstOrDefaultAsync(cancellationToken)
                    ?? string.Empty;

                if (string.IsNullOrWhiteSpace(cinemaName))
                {
                    return ScopeResolution.Fail(
                        "Cinema was not found.",
                        BookingConstants.StaffShiftReportErrorCode.CinemaNotFound);
                }
            }

            return ScopeResolution.Ok(new ReportScope(
                cinemaId,
                cinemaName,
                targetStaff?.StaffProfileId,
                targetStaff?.StaffName ?? AllStaffLabel));
        }

        return ScopeResolution.Fail(
            "Role is not allowed to view staff shift reports.",
            BookingConstants.StaffShiftReportErrorCode.RoleForbidden);
    }

    private Task<List<StaffShiftReportTransactionResponse>> QueryCheckInTransactionsAsync(
        ReportScope reportScope,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.CheckinLogs
            .AsNoTracking()
            .Where(log =>
                log.Result == BookingConstants.CheckInResult.Success
                && log.StaffProfileId != null
                && log.ScanTime >= from
                && log.ScanTime < to);

        if (reportScope.StaffProfileId is not null)
        {
            query = query.Where(log => log.StaffProfileId == reportScope.StaffProfileId);
        }
        else if (reportScope.CinemaId is not null)
        {
            query = query.Where(log => log.StaffProfile!.CinemaId == reportScope.CinemaId);
        }

        return query
            .Select(log => new StaffShiftReportTransactionResponse
            {
                OccurredAt = log.ScanTime,
                TransactionType = StaffShiftReportTransactionTypes.TicketCheckIn,
                ReferenceId = log.CheckInLogId,
                StaffProfileId = log.StaffProfileId,
                StaffName = log.StaffProfile!.User.FullName,
                CinemaId = log.StaffProfile.CinemaId,
                CinemaName = log.StaffProfile.Cinema.CinemaName,
                Amount = 0m,
                Description = log.TicketId == null
                    ? "Ticket check-in"
                    : $"Ticket check-in {log.TicketId}"
            })
            .ToListAsync(cancellationToken);
    }

    private Task<List<StaffShiftReportTransactionResponse>> QueryCounterTransactionsAsync(
        ReportScope reportScope,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Bookings
            .AsNoTracking()
            .Where(booking =>
                booking.BookingChannel == BookingConstants.BookingChannel.Counter
                && booking.CreatedByStaffProfileId != null
                && booking.CreatedAt >= from
                && booking.CreatedAt < to
                && (booking.BookingStatus == BookingConstants.BookingStatus.Paid
                    || booking.BookingStatus == BookingConstants.BookingStatus.Completed));

        if (reportScope.StaffProfileId is not null)
        {
            query = query.Where(booking => booking.CreatedByStaffProfileId == reportScope.StaffProfileId);
        }
        else if (reportScope.CinemaId is not null)
        {
            query = query.Where(booking => booking.CreatedByStaffProfile!.CinemaId == reportScope.CinemaId);
        }

        return query
            .Select(booking => new StaffShiftReportTransactionResponse
            {
                OccurredAt = booking.CreatedAt,
                TransactionType = StaffShiftReportTransactionTypes.CounterFbOrder,
                ReferenceId = booking.BookingId,
                StaffProfileId = booking.CreatedByStaffProfileId,
                StaffName = booking.CreatedByStaffProfile!.User.FullName,
                CinemaId = booking.CreatedByStaffProfile.CinemaId,
                CinemaName = booking.CreatedByStaffProfile.Cinema.CinemaName,
                Amount = booking.TotalAmount,
                PaymentMethod = booking.Payments
                    .Where(payment => payment.PaymentStatus == BookingConstants.PaymentStatus.Success)
                    .OrderByDescending(payment => payment.PaidAt ?? payment.CreatedAt)
                    .Select(payment => payment.PaymentMethod)
                    .FirstOrDefault(),
                Description = "Counter F&B order"
            })
            .ToListAsync(cancellationToken);
    }

    private Task<List<StaffShiftReportTransactionResponse>> QueryOnlineFulfillmentTransactionsAsync(
        ReportScope reportScope,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Bookings
            .AsNoTracking()
            .Where(booking =>
                booking.BookingChannel == BookingConstants.BookingChannel.Online
                && booking.FbFulfilledByStaffProfileId != null
                && booking.FbFulfillmentStatus == FbConstants.FulfillmentStatus.Fulfilled
                && booking.FbFulfilledAt.HasValue
                && booking.FbFulfilledAt.Value >= from
                && booking.FbFulfilledAt.Value < to);

        if (reportScope.StaffProfileId is not null)
        {
            query = query.Where(booking => booking.FbFulfilledByStaffProfileId == reportScope.StaffProfileId);
        }
        else if (reportScope.CinemaId is not null)
        {
            query = query.Where(booking => booking.FbFulfilledByStaffProfile!.CinemaId == reportScope.CinemaId);
        }

        return query
            .Select(booking => new StaffShiftReportTransactionResponse
            {
                OccurredAt = booking.FbFulfilledAt!.Value,
                TransactionType = StaffShiftReportTransactionTypes.OnlineFbFulfillment,
                ReferenceId = booking.BookingId,
                StaffProfileId = booking.FbFulfilledByStaffProfileId,
                StaffName = booking.FbFulfilledByStaffProfile!.User.FullName,
                CinemaId = booking.FbFulfilledByStaffProfile.CinemaId,
                CinemaName = booking.FbFulfilledByStaffProfile.Cinema.CinemaName,
                Amount = booking.BookingFbItems.Sum(item => item.Subtotal),
                Description = "Online F&B fulfillment"
            })
            .ToListAsync(cancellationToken);
    }

    private Task<StaffProfileProjection?> GetCurrentStaffProfileAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        return _dbContext.StaffProfiles
            .AsNoTracking()
            .Where(profile =>
                profile.UserId == userId
                && profile.EmploymentStatus == BookingConstants.ResourceStatus.Active)
            .Select(profile => new StaffProfileProjection(
                profile.StaffProfileId,
                profile.User.FullName,
                profile.CinemaId,
                profile.Cinema.CinemaName))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private Task<StaffProfileProjection?> GetStaffProfileAsync(
        string staffProfileId,
        CancellationToken cancellationToken)
    {
        return _dbContext.StaffProfiles
            .AsNoTracking()
            .Where(profile =>
                profile.StaffProfileId == staffProfileId
                && profile.EmploymentStatus == BookingConstants.ResourceStatus.Active)
            .Select(profile => new StaffProfileProjection(
                profile.StaffProfileId,
                profile.User.FullName,
                profile.CinemaId,
                profile.Cinema.CinemaName))
            .FirstOrDefaultAsync(cancellationToken);
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

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool EqualsIgnoreCase(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCash(string? paymentMethod)
    {
        if (string.IsNullOrWhiteSpace(paymentMethod))
        {
            return false;
        }

        var normalized = paymentMethod.Trim().ToUpperInvariant();
        return normalized.Contains("CASH", StringComparison.Ordinal)
            || normalized.Contains("TIEN_MAT", StringComparison.Ordinal);
    }

    private static bool IsTransfer(string? paymentMethod)
    {
        if (string.IsNullOrWhiteSpace(paymentMethod))
        {
            return false;
        }

        var normalized = paymentMethod.Trim().ToUpperInvariant();
        return normalized.Contains("TRANSFER", StringComparison.Ordinal)
            || normalized.Contains("BANK", StringComparison.Ordinal)
            || normalized.Contains("SEPAY", StringComparison.Ordinal)
            || normalized.Contains("CHUYEN_KHOAN", StringComparison.Ordinal);
    }

    private sealed record StaffProfileProjection(
        string StaffProfileId,
        string StaffName,
        string CinemaId,
        string CinemaName);

    private sealed record ReportScope(
        string? CinemaId,
        string CinemaName,
        string? StaffProfileId,
        string StaffName);

    private sealed record ScopeResolution(
        ReportScope? Scope,
        ApiResponse<StaffShiftReportResponse>? Error)
    {
        public static ScopeResolution Ok(ReportScope scope)
        {
            return new ScopeResolution(scope, null);
        }

        public static ScopeResolution Fail(string message, string errorCode)
        {
            return new ScopeResolution(
                null,
                ApiResponse<StaffShiftReportResponse>.Fail(message, errorCode));
        }
    }

    private static class StaffShiftReportTransactionTypes
    {
        public const string TicketCheckIn = "TICKET_CHECK_IN";
        public const string CounterFbOrder = "COUNTER_FB_ORDER";
        public const string OnlineFbFulfillment = "ONLINE_FB_FULFILLMENT";
    }
}
