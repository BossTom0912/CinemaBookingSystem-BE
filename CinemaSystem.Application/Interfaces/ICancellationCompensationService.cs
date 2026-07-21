using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Compensations;
using CinemaSystem.Domain.Entities;

namespace CinemaSystem.Application.Interfaces;

public interface ICancellationCompensationService
{
    Task<CompensationIssueResult> IssueForCancelledBookingAsync(
        Booking booking,
        string showtimeCancellationId,
        DateTime now,
        CancellationToken cancellationToken);

    Task<ServiceResult<decimal>> ReserveTicketsAsync(
        string? customerProfileId,
        string? guestEmail,
        string? guestPhone,
        IReadOnlyList<string> voucherCodes,
        string bookingId,
        IReadOnlyList<BookingSeat> bookingSeats,
        DateTime now,
        CancellationToken cancellationToken);

    Task ConfirmBookingReservationsAsync(
        string bookingId,
        DateTime now,
        CancellationToken cancellationToken);

    Task ReleaseBookingReservationsAsync(
        string bookingId,
        CancellationToken cancellationToken);

    Task RestoreBookingEntitlementsAsync(
        string bookingId,
        CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyList<CompensationResponse>>> GetMineAsync(
        string userId,
        CancellationToken cancellationToken);

    Task<ServiceResult<RedeemCompensationComboResponse>> RedeemComboAsync(
        string voucherCode,
        string actorUserId,
        DateTime now,
        CancellationToken cancellationToken);
}
