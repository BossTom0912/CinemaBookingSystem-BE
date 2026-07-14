using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Bookings;

namespace CinemaSystem.Application.Interfaces;

public interface IBookingService
{
    Task<ServiceResult<BookingResponse>> CreateBookingAsync(
        CreateBookingRequest request,
        string userId,
        Guid? clientRequestId,
        CancellationToken cancellationToken);

    Task<ServiceResult<CheckoutRecoveryResponse>> GetCheckoutRecoveryAsync(
        string userId,
        Guid clientRequestId,
        CancellationToken cancellationToken);

    Task<ServiceResult<BookingDetailsResponse>> CreateCounterBookingAsync(
        CreateCounterBookingRequest request,
        string staffProfileId,
        string currentStaffCinemaId,
        CancellationToken cancellationToken);

    Task<ServiceResult<BookingDetailsResponse>> GetBookingDetailsAsync(
        string bookingId,
        string userId,
        CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyList<BookingResponse>>> GetMyBookingsAsync(
        string userId,
        CancellationToken cancellationToken);

    Task<ServiceResult<bool>> CancelPendingBookingAsync(
        string bookingId,
        string userId,
        CancellationToken cancellationToken);

    Task<ServiceResult<bool>> ConfirmTimeChangeAsync(
        string bookingId,
        bool accept,
        string token,
        CancellationToken cancellationToken);

    Task<ServiceResult<bool>> ReassignBookingSeatAsync(
        ReassignSeatRequest request,
        CancellationToken cancellationToken);
}
