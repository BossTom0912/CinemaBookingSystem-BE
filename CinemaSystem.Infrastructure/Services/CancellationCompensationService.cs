using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Compensations;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Infrastructure.Services;

public sealed class CancellationCompensationService : ICancellationCompensationService
{
    private readonly CinemaDbContext _dbContext;
    private readonly CancellationCompensationSettings _settings;
    private readonly IClock _clock;

    public CancellationCompensationService(
        CinemaDbContext dbContext,
        IOptions<CancellationCompensationSettings> settings,
        IClock clock)
    {
        _dbContext = dbContext;
        _settings = settings.Value;
        _clock = clock;
    }

    public async Task<CompensationIssueResult> IssueForCancelledBookingAsync(
        Booking booking,
        string showtimeCancellationId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existing = _dbContext.CancellationCompensations.Local
            .FirstOrDefault(item => item.SourceBookingId == booking.BookingId)
            ?? await _dbContext.CancellationCompensations
                .Include(item => item.Tickets)
                .Include(item => item.Combo)
                .FirstOrDefaultAsync(
                    item => item.SourceBookingId == booking.BookingId,
                    cancellationToken);

        if (existing is not null)
        {
            var existingEntry = _dbContext.Entry(existing);
            if (existingEntry.State != EntityState.Added
                && !existingEntry.Collection(item => item.Tickets).IsLoaded)
            {
                await existingEntry
                    .Collection(item => item.Tickets)
                    .LoadAsync(cancellationToken);
            }

            if (existingEntry.State != EntityState.Added
                && !existingEntry.Reference(item => item.Combo).IsLoaded)
            {
                await existingEntry
                    .Reference(item => item.Combo)
                    .LoadAsync(cancellationToken);
            }

            return new CompensationIssueResult(
                existing.CancellationCompensationId,
                existing.Tickets.Count,
                existing.Combo is null ? 0 : 1,
                existing.ExpiresAt,
                true,
                existing.Tickets.Select(item => item.VoucherCode).ToList(),
                existing.Combo?.VoucherCode);
        }

        var expiresAt = now.AddDays(_settings.ValidityDays);
        var compensation = new CancellationCompensation
        {
            CancellationCompensationId = NewId(
                DomainConstants.EntityIdPrefix.CancellationCompensation),
            SourceBookingId = booking.BookingId,
            ShowtimeCancellationId = showtimeCancellationId,
            CustomerProfileId = booking.CustomerProfileId,
            Status = DomainConstants.CancellationCompensationStatus.Issued,
            PolicyVersion = DomainConstants.CancellationCompensationPolicy.Version,
            IssuedAt = now,
            ExpiresAt = expiresAt,
            SourceBooking = booking
        };

        foreach (var _ in booking.BookingSeats)
        {
            compensation.Tickets.Add(new CompensationTicket
            {
                CompensationTicketId = NewId(
                    DomainConstants.EntityIdPrefix.CompensationTicket),
                VoucherCode = NewVoucherCode("TICKET"),
                Status = DomainConstants.CompensationEntitlementStatus.Issued
            });
        }

        compensation.Combo = new CompensationCombo
        {
            CompensationComboId = NewId(
                DomainConstants.EntityIdPrefix.CompensationCombo),
            VoucherCode = NewVoucherCode("COMBO"),
            DisplayName = _settings.ComboDisplayName,
            Status = DomainConstants.CompensationEntitlementStatus.Issued
        };

        _dbContext.CancellationCompensations.Add(compensation);

        return new CompensationIssueResult(
            compensation.CancellationCompensationId,
            compensation.Tickets.Count,
            1,
            compensation.ExpiresAt,
            false,
            compensation.Tickets.Select(item => item.VoucherCode).ToList(),
            compensation.Combo.VoucherCode);
    }

    public async Task<ServiceResult<decimal>> ReserveTicketsAsync(
        string? customerProfileId,
        string? guestEmail,
        string? guestPhone,
        IReadOnlyList<string> voucherCodes,
        string bookingId,
        IReadOnlyList<BookingSeat> bookingSeats,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (voucherCodes.Count == 0)
        {
            return ServiceResult<decimal>.Ok(0m);
        }

        var normalizedCodes = voucherCodes
            .Select(NormalizeCode)
            .ToList();
        if (normalizedCodes.Any(string.IsNullOrWhiteSpace)
            || normalizedCodes.Distinct(StringComparer.Ordinal).Count() != normalizedCodes.Count)
        {
            return ServiceResult<decimal>.Fail(
                400,
                "Compensation ticket codes must be non-empty and unique.",
                "INVALID_COMPENSATION_TICKET_CODES");
        }

        if (normalizedCodes.Count > bookingSeats.Count)
        {
            return ServiceResult<decimal>.Fail(
                400,
                "A compensation ticket can only be applied to one selected seat.",
                "COMPENSATION_TICKET_COUNT_EXCEEDED");
        }

        var tickets = await _dbContext.CompensationTickets
            .Include(item => item.CancellationCompensation)
                .ThenInclude(item => item.SourceBooking)
            .Where(item => normalizedCodes.Contains(item.VoucherCode))
            .ToListAsync(cancellationToken);

        if (tickets.Count != normalizedCodes.Count)
        {
            return ServiceResult<decimal>.Fail(
                404,
                "One or more compensation ticket codes were not found.",
                "COMPENSATION_TICKET_NOT_FOUND");
        }

        var ticketsByCode = tickets.ToDictionary(
            item => item.VoucherCode,
            StringComparer.Ordinal);
        decimal discountAmount = 0m;
        for (var index = 0; index < normalizedCodes.Count; index++)
        {
            var ticket = ticketsByCode[normalizedCodes[index]];
            if (!IsOwnedBy(
                    ticket.CancellationCompensation,
                    customerProfileId,
                    guestEmail,
                    guestPhone))
            {
                return ServiceResult<decimal>.Fail(
                    403,
                    "This compensation ticket does not belong to this customer.",
                    "COMPENSATION_TICKET_FORBIDDEN");
            }

            if (ticket.CancellationCompensation.ExpiresAt <= now)
            {
                ticket.Status = DomainConstants.CompensationEntitlementStatus.Expired;
                return ServiceResult<decimal>.Fail(
                    410,
                    "This compensation ticket has expired.",
                    "COMPENSATION_TICKET_EXPIRED");
            }

            if (!string.Equals(
                    ticket.Status,
                    DomainConstants.CompensationEntitlementStatus.Issued,
                    StringComparison.OrdinalIgnoreCase))
            {
                return ServiceResult<decimal>.Fail(
                    409,
                    "This compensation ticket is not available.",
                    "COMPENSATION_TICKET_UNAVAILABLE");
            }

            var bookingSeat = bookingSeats[index];
            ticket.Status = DomainConstants.CompensationEntitlementStatus.Reserved;
            ticket.ReservedBookingId = bookingId;
            ticket.ReservedBookingSeatId = bookingSeat.BookingSeatId;
            ticket.ReservedBookingSeat = bookingSeat;
            ticket.ReservedAt = now;
            discountAmount += bookingSeat.SeatPrice;
        }

        return ServiceResult<decimal>.Ok(
            discountAmount,
            "Compensation tickets reserved successfully.");
    }

    public async Task ConfirmBookingReservationsAsync(
        string bookingId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var tickets = await GetReservedTicketsForBookingAsync(
            bookingId,
            cancellationToken);
        if (tickets.Count == 0)
        {
            return;
        }

        foreach (var ticket in tickets)
        {
            ticket.Status = DomainConstants.CompensationEntitlementStatus.Redeemed;
            ticket.RedeemedAt = now;
        }

        await RefreshParentStatusesAsync(
            tickets.Select(item => item.CancellationCompensationId),
            cancellationToken);
    }

    public async Task ReleaseBookingReservationsAsync(
        string bookingId,
        CancellationToken cancellationToken)
    {
        var tickets = await GetReservedTicketsForBookingAsync(
            bookingId,
            cancellationToken);
        if (tickets.Count == 0)
        {
            return;
        }

        foreach (var ticket in tickets)
        {
            ticket.Status = DomainConstants.CompensationEntitlementStatus.Issued;
            ticket.ReservedBookingId = null;
            ticket.ReservedBookingSeatId = null;
            ticket.ReservedBooking = null;
            ticket.ReservedBookingSeat = null;
            ticket.ReservedAt = null;
        }

        await RefreshParentStatusesAsync(
            tickets.Select(item => item.CancellationCompensationId),
            cancellationToken);
    }

    public async Task RestoreBookingEntitlementsAsync(
        string bookingId,
        CancellationToken cancellationToken)
    {
        var tickets = _dbContext.CompensationTickets.Local
            .Where(item => item.ReservedBookingId == bookingId
                && item.Status is DomainConstants.CompensationEntitlementStatus.Reserved
                    or DomainConstants.CompensationEntitlementStatus.Redeemed)
            .ToList();
        var localIds = tickets
            .Select(item => item.CompensationTicketId)
            .ToHashSet(StringComparer.Ordinal);
        tickets.AddRange(await _dbContext.CompensationTickets
            .Where(item => item.ReservedBookingId == bookingId
                && (item.Status == DomainConstants.CompensationEntitlementStatus.Reserved
                    || item.Status == DomainConstants.CompensationEntitlementStatus.Redeemed)
                && !localIds.Contains(item.CompensationTicketId))
            .ToListAsync(cancellationToken));

        foreach (var ticket in tickets)
        {
            ticket.Status = DomainConstants.CompensationEntitlementStatus.Issued;
            ticket.ReservedBookingId = null;
            ticket.ReservedBookingSeatId = null;
            ticket.ReservedBooking = null;
            ticket.ReservedBookingSeat = null;
            ticket.ReservedAt = null;
            ticket.RedeemedAt = null;
        }

        await RefreshParentStatusesAsync(
            tickets.Select(item => item.CancellationCompensationId),
            cancellationToken);
    }

    public async Task<ServiceResult<IReadOnlyList<CompensationResponse>>> GetMineAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var customerProfileId = await _dbContext.CustomerProfiles
            .Where(item => item.UserId == userId)
            .Select(item => item.CustomerProfileId)
            .FirstOrDefaultAsync(cancellationToken);
        if (customerProfileId is null)
        {
            return ServiceResult<IReadOnlyList<CompensationResponse>>.Fail(
                403,
                "Customer profile was not found.",
                "CUSTOMER_PROFILE_NOT_FOUND");
        }

        var now = _clock.UtcNow;
        var compensations = await _dbContext.CancellationCompensations
            .Include(item => item.Tickets)
            .Include(item => item.Combo)
            .Where(item => item.CustomerProfileId == customerProfileId)
            .OrderByDescending(item => item.IssuedAt)
            .ToListAsync(cancellationToken);

        var changed = false;
        foreach (var compensation in compensations.Where(item => item.ExpiresAt <= now))
        {
            foreach (var ticket in compensation.Tickets.Where(item =>
                         item.Status == DomainConstants.CompensationEntitlementStatus.Issued))
            {
                ticket.Status = DomainConstants.CompensationEntitlementStatus.Expired;
                changed = true;
            }

            if (compensation.Combo?.Status
                == DomainConstants.CompensationEntitlementStatus.Issued)
            {
                compensation.Combo.Status =
                    DomainConstants.CompensationEntitlementStatus.Expired;
                changed = true;
            }

            if (compensation.Tickets.All(item =>
                    item.Status is DomainConstants.CompensationEntitlementStatus.Expired
                        or DomainConstants.CompensationEntitlementStatus.Redeemed)
                && compensation.Combo?.Status
                    is DomainConstants.CompensationEntitlementStatus.Expired
                        or DomainConstants.CompensationEntitlementStatus.Redeemed)
            {
                compensation.Status =
                    DomainConstants.CancellationCompensationStatus.Expired;
            }
        }

        if (changed)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var response = compensations.Select(Map).ToList();
        return ServiceResult<IReadOnlyList<CompensationResponse>>.Ok(
            response,
            "Cancellation compensations retrieved successfully.");
    }

    public async Task<ServiceResult<RedeemCompensationComboResponse>> RedeemComboAsync(
        string voucherCode,
        string actorUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var staff = await _dbContext.StaffProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.UserId == actorUserId
                    && item.EmploymentStatus == DomainConstants.StaffEmploymentStatus.Active,
                cancellationToken);
        if (staff is null)
        {
            return ServiceResult<RedeemCompensationComboResponse>.Fail(
                403,
                "An active staff profile is required to redeem a combo.",
                "ACTIVE_STAFF_PROFILE_REQUIRED");
        }

        var normalizedCode = NormalizeCode(voucherCode);
        var combo = await _dbContext.CompensationCombos
            .Include(item => item.CancellationCompensation)
            .FirstOrDefaultAsync(
                item => item.VoucherCode == normalizedCode,
                cancellationToken);
        if (combo is null)
        {
            return ServiceResult<RedeemCompensationComboResponse>.Fail(
                404,
                "Compensation combo code was not found.",
                "COMPENSATION_COMBO_NOT_FOUND");
        }

        if (combo.CancellationCompensation.ExpiresAt <= now)
        {
            combo.Status = DomainConstants.CompensationEntitlementStatus.Expired;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<RedeemCompensationComboResponse>.Fail(
                410,
                "This compensation combo has expired.",
                "COMPENSATION_COMBO_EXPIRED");
        }

        if (!string.Equals(
                combo.Status,
                DomainConstants.CompensationEntitlementStatus.Issued,
                StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<RedeemCompensationComboResponse>.Fail(
                409,
                "This compensation combo is not available.",
                "COMPENSATION_COMBO_UNAVAILABLE");
        }

        combo.Status = DomainConstants.CompensationEntitlementStatus.Redeemed;
        combo.RedeemedAt = now;
        combo.RedeemedAtCinemaId = staff.CinemaId;
        combo.RedeemedByStaffProfileId = staff.StaffProfileId;

        await RefreshParentStatusesAsync(
            [combo.CancellationCompensationId],
            cancellationToken);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<RedeemCompensationComboResponse>.Fail(
                409,
                "This compensation combo was redeemed by another request.",
                "COMPENSATION_COMBO_ALREADY_REDEEMED");
        }

        return ServiceResult<RedeemCompensationComboResponse>.Ok(
            new RedeemCompensationComboResponse
            {
                CompensationComboId = combo.CompensationComboId,
                VoucherCode = combo.VoucherCode,
                DisplayName = combo.DisplayName,
                CinemaId = staff.CinemaId,
                RedeemedAt = now
            },
            "Compensation combo redeemed successfully.");
    }

    private async Task<List<CompensationTicket>> GetReservedTicketsForBookingAsync(
        string bookingId,
        CancellationToken cancellationToken)
    {
        var local = _dbContext.CompensationTickets.Local
            .Where(item => item.ReservedBookingId == bookingId
                && item.Status == DomainConstants.CompensationEntitlementStatus.Reserved)
            .ToList();
        var localIds = local
            .Select(item => item.CompensationTicketId)
            .ToHashSet(StringComparer.Ordinal);
        var persisted = await _dbContext.CompensationTickets
            .Where(item => item.ReservedBookingId == bookingId
                && item.Status == DomainConstants.CompensationEntitlementStatus.Reserved
                && !localIds.Contains(item.CompensationTicketId))
            .ToListAsync(cancellationToken);
        local.AddRange(persisted);
        return local;
    }

    private async Task RefreshParentStatusesAsync(
        IEnumerable<string> compensationIds,
        CancellationToken cancellationToken)
    {
        foreach (var compensationId in compensationIds.Distinct(StringComparer.Ordinal))
        {
            var compensation = _dbContext.CancellationCompensations.Local
                .FirstOrDefault(item =>
                    item.CancellationCompensationId == compensationId)
                ?? await _dbContext.CancellationCompensations
                    .Include(item => item.Tickets)
                    .Include(item => item.Combo)
                    .FirstAsync(
                        item => item.CancellationCompensationId == compensationId,
                        cancellationToken);

            if (!_dbContext.Entry(compensation).Collection(item => item.Tickets).IsLoaded)
            {
                await _dbContext.Entry(compensation)
                    .Collection(item => item.Tickets)
                    .LoadAsync(cancellationToken);
            }

            if (!_dbContext.Entry(compensation).Reference(item => item.Combo).IsLoaded)
            {
                await _dbContext.Entry(compensation)
                    .Reference(item => item.Combo)
                    .LoadAsync(cancellationToken);
            }

            var usedCount = compensation.Tickets.Count(item =>
                    item.Status == DomainConstants.CompensationEntitlementStatus.Redeemed)
                + (compensation.Combo?.Status
                    == DomainConstants.CompensationEntitlementStatus.Redeemed
                    ? 1
                    : 0);
            var entitlementCount = compensation.Tickets.Count
                + (compensation.Combo is null ? 0 : 1);

            compensation.Status = usedCount switch
            {
                0 => DomainConstants.CancellationCompensationStatus.Issued,
                _ when usedCount == entitlementCount =>
                    DomainConstants.CancellationCompensationStatus.Used,
                _ => DomainConstants.CancellationCompensationStatus.PartiallyUsed
            };
        }
    }

    private static CompensationResponse Map(CancellationCompensation item)
    {
        return new CompensationResponse
        {
            CompensationId = item.CancellationCompensationId,
            SourceBookingId = item.SourceBookingId,
            Status = item.Status,
            PolicyVersion = item.PolicyVersion,
            IssuedAt = item.IssuedAt,
            ExpiresAt = item.ExpiresAt,
            Tickets = item.Tickets
                .OrderBy(ticket => ticket.CompensationTicketId)
                .Select(ticket => new CompensationTicketResponse
                {
                    CompensationTicketId = ticket.CompensationTicketId,
                    VoucherCode = ticket.VoucherCode,
                    Status = ticket.Status,
                    ReservedBookingId = ticket.ReservedBookingId,
                    RedeemedAt = ticket.RedeemedAt
                })
                .ToList(),
            Combo = item.Combo is null
                ? null
                : new CompensationComboResponse
                {
                    CompensationComboId = item.Combo.CompensationComboId,
                    VoucherCode = item.Combo.VoucherCode,
                    DisplayName = item.Combo.DisplayName,
                    Status = item.Combo.Status,
                    RedeemedAt = item.Combo.RedeemedAt,
                    RedeemedAtCinemaId = item.Combo.RedeemedAtCinemaId
                }
        };
    }

    private static string NormalizeCode(string? code) =>
        (code ?? string.Empty).Trim().ToUpperInvariant();

    private static bool IsOwnedBy(
        CancellationCompensation compensation,
        string? customerProfileId,
        string? guestEmail,
        string? guestPhone)
    {
        if (!string.IsNullOrWhiteSpace(compensation.CustomerProfileId))
        {
            return string.Equals(
                compensation.CustomerProfileId,
                customerProfileId?.Trim(),
                StringComparison.Ordinal);
        }

        var sourceBooking = compensation.SourceBooking;
        return !string.IsNullOrWhiteSpace(sourceBooking.GuestEmail)
            && !string.IsNullOrWhiteSpace(sourceBooking.GuestPhone)
            && string.Equals(
                sourceBooking.GuestEmail.Trim(),
                guestEmail?.Trim(),
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                NormalizePhone(sourceBooking.GuestPhone),
                NormalizePhone(guestPhone),
                StringComparison.Ordinal);
    }

    private static string NormalizePhone(string? phone) =>
        new((phone ?? string.Empty).Where(char.IsDigit).ToArray());

    private static string NewId(string prefix) =>
        $"{prefix}_{Guid.NewGuid():N}";

    private static string NewVoucherCode(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}".ToUpperInvariant();
}
