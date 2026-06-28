using System.Data;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Bookings;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Infrastructure.Bookings;

/// <summary>
/// Runtime implementation of the transactional checkout use case reached from
/// <c>BookingsController.Checkout</c> through <see cref="ICheckoutService"/>.
/// </summary>
/// <remarks>
/// Within a SQL transaction this class validates the customer, showtime cutoff,
/// seat-lock ownership, F&amp;B inventory and voucher rules, then creates
/// BOOKING/BOOKING_SEAT plus optional BOOKING_FB_ITEM/VOUCHER_USAGE records.
/// It returns a PENDING_PAYMENT booking; SePay payment and ticket issuance are
/// handled afterward by <c>PaymentService</c>.
/// </remarks>
public sealed class CheckoutService : ICheckoutService
{
    private readonly CinemaDbContext _dbContext;
    private readonly IClock _clock;
    private readonly BookingSettings _settings;
    private readonly ILogger<CheckoutService> _logger;

    public CheckoutService(
        CinemaDbContext dbContext,
        IClock clock,
        IOptions<BookingSettings> settings,
        ILogger<CheckoutService> logger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<ServiceResult<CheckoutResponse>> CheckoutAsync(
        string userId,
        CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Fail(401, "Unauthorized.", BookingConstants.ErrorCodes.Unauthorized);
        }

        var seatIds = NormalizeIds(request.ShowtimeSeatIds);
        var foodItems = request.FoodItems ?? [];

        var requestValidation = ValidateRequest(request, seatIds, foodItems);
        if (requestValidation is not null)
        {
            return requestValidation;
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        try
        {
            var customer = await _dbContext.CustomerProfiles
                .AsNoTracking()
                .Include(item => item.User)
                .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);

            if (customer is null)
            {
                return await RollbackAndFailAsync(
                    transaction,
                    404,
                    "Customer profile was not found.",
                    BookingConstants.ErrorCodes.CustomerProfileNotFound,
                    cancellationToken);
            }

            if (!customer.User.EmailVerified ||
                !string.Equals(
                    customer.User.Status,
                    AuthConstants.UserStatus.Active,
                    StringComparison.OrdinalIgnoreCase))
            {
                return await RollbackAndFailAsync(
                    transaction,
                    403,
                    "This account is not allowed to create bookings.",
                    BookingConstants.ErrorCodes.BookingNotAllowed,
                    cancellationToken);
            }

            var showtime = await _dbContext.Showtimes
                .AsNoTracking()
                .Include(item => item.Movie)
                .Include(item => item.Room)
                .ThenInclude(item => item.Cinema)
                .SingleOrDefaultAsync(item => item.ShowtimeId == request.ShowtimeId.Trim(), cancellationToken);

            if (showtime is null)
            {
                return await RollbackAndFailAsync(
                    transaction,
                    404,
                    "Showtime was not found.",
                    BookingConstants.ErrorCodes.ShowtimeNotFound,
                    cancellationToken);
            }

            var now = _clock.UtcNow;
            if (!IsShowtimeBookable(showtime))
            {
                return await RollbackAndFailAsync(
                    transaction,
                    409,
                    "Showtime is not open for booking.",
                    BookingConstants.ErrorCodes.ShowtimeNotOpen,
                    cancellationToken);
            }

            var cutoffMinutes = Math.Max(0, _settings.OnlineSaleCutoffMinutes);
            if (showtime.StartTime <= now.AddMinutes(cutoffMinutes))
            {
                return await RollbackAndFailAsync(
                    transaction,
                    409,
                    "Online ticket sales are closed for this showtime.",
                    BookingConstants.ErrorCodes.OnlineSaleClosed,
                    cancellationToken);
            }

            var showtimeSeats = await _dbContext.ShowtimeSeats
                .Include(item => item.Seat)
                .ThenInclude(item => item.SeatType)
                .Include(item => item.BookingSeat)
                .Where(item => seatIds.Contains(item.ShowtimeSeatId))
                .ToListAsync(cancellationToken);

            var seatValidation = ValidateSeats(showtimeSeats, seatIds, showtime.ShowtimeId, userId, now);
            if (seatValidation is not null)
            {
                return await RollbackAndFailAsync(
                    transaction,
                    seatValidation.StatusCode,
                    seatValidation.Message,
                    seatValidation.ErrorCode!,
                    cancellationToken);
            }

            var seatResponses = showtimeSeats
                .OrderBy(item => item.Seat.RowLabel)
                .ThenBy(item => item.Seat.SeatNumber)
                .Select(item => new CheckoutSeatResponse
                {
                    ShowtimeSeatId = item.ShowtimeSeatId,
                    SeatCode = item.Seat.SeatCode,
                    SeatType = item.Seat.SeatType.TypeName,
                    Price = showtime.BasePrice + item.Seat.SeatType.ExtraFee
                })
                .ToList();
            var seatSubtotal = seatResponses.Sum(item => item.Price);

            var foodResult = await LoadAndValidateFoodItemsAsync(
                showtime.Room.CinemaId,
                foodItems,
                cancellationToken);
            if (!foodResult.Success)
            {
                return await RollbackAndFailAsync(
                    transaction,
                    foodResult.StatusCode,
                    foodResult.Message,
                    foodResult.ErrorCode!,
                    cancellationToken);
            }

            var foodResponses = foodResult.Data!;
            var foodSubtotal = foodResponses.Sum(item => item.Subtotal);
            var grossAmount = seatSubtotal + foodSubtotal;

            Voucher? voucher = null;
            decimal voucherDiscount = 0;
            if (!string.IsNullOrWhiteSpace(request.VoucherCode))
            {
                var voucherResult = await ValidateVoucherAsync(
                    request.VoucherCode.Trim(),
                    customer.CustomerProfileId,
                    grossAmount,
                    now,
                    cancellationToken);
                if (!voucherResult.Success)
                {
                    return await RollbackAndFailAsync(
                        transaction,
                        voucherResult.StatusCode,
                        voucherResult.Message,
                        voucherResult.ErrorCode!,
                        cancellationToken);
                }

                voucher = voucherResult.Data!.Voucher;
                voucherDiscount = voucherResult.Data.DiscountAmount;
            }

            var totalAmount = Math.Max(0m, grossAmount - voucherDiscount);
            var expiresAt = showtimeSeats.Min(item => item.LockedUntil!.Value);
            if (expiresAt <= _clock.UtcNow)
            {
                return await RollbackAndFailAsync(
                    transaction,
                    409,
                    "One or more seat locks have expired.",
                    BookingConstants.ErrorCodes.SeatLockExpired,
                    cancellationToken);
            }

            var bookingId = NewId("BKG");
            var booking = new Booking
            {
                BookingId = bookingId,
                CustomerProfileId = customer.CustomerProfileId,
                ShowtimeId = showtime.ShowtimeId,
                BookingStatus = BookingConstants.BookingStatus.PendingPayment,
                BookingChannel = BookingConstants.BookingChannel.Online,
                TotalAmount = totalAmount,
                CreatedAt = now,
                ExpiredAt = expiresAt
            };

            foreach (var seat in showtimeSeats)
            {
                booking.BookingSeats.Add(new BookingSeat
                {
                    BookingSeatId = NewId("BKS"),
                    BookingId = bookingId,
                    ShowtimeSeatId = seat.ShowtimeSeatId,
                    SeatPrice = showtime.BasePrice + seat.Seat.SeatType.ExtraFee
                });

                _dbContext.Entry(seat).Property(item => item.LockedUntil).IsModified = true;
            }

            foreach (var food in foodResponses)
            {
                booking.BookingFbItems.Add(new BookingFbItem
                {
                    BookingFbitemId = NewId("BFI"),
                    BookingId = bookingId,
                    FbItemId = food.FbItemId,
                    Quantity = food.Quantity,
                    UnitPrice = food.UnitPrice,
                    Subtotal = food.Subtotal
                });
            }

            if (voucher is not null)
            {
                booking.VoucherUsage = new VoucherUsage
                {
                    VoucherUsageId = NewId("VUS"),
                    VoucherId = voucher.VoucherId,
                    CustomerProfileId = customer.CustomerProfileId,
                    BookingId = bookingId,
                    DiscountAmount = voucherDiscount,
                    UsageStatus = BookingConstants.VoucherUsageStatus.Applied,
                    UsedAt = null
                };
            }

            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Checkout {BookingId} created for user {UserId}, showtime {ShowtimeId}, seats {SeatCount}, total {TotalAmount}.",
                bookingId,
                userId,
                showtime.ShowtimeId,
                showtimeSeats.Count,
                totalAmount);

            return ServiceResult<CheckoutResponse>.Ok(
                new CheckoutResponse
                {
                    BookingId = bookingId,
                    BookingStatus = BookingConstants.BookingStatus.PendingPayment,
                    ShowtimeId = showtime.ShowtimeId,
                    Seats = seatResponses,
                    FoodItems = foodResponses,
                    SeatSubtotal = seatSubtotal,
                    FoodSubtotal = foodSubtotal,
                    GrossAmount = grossAmount,
                    VoucherDiscount = voucherDiscount,
                    RewardDiscount = 0,
                    TotalAmount = totalAmount,
                    ExpiredAt = expiresAt
                },
                "Checkout created successfully.",
                201);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            await RollbackSafelyAsync(transaction);
            _logger.LogWarning(exception, "Checkout failed because a seat lock changed concurrently.");
            return Fail(
                409,
                "Seat state changed while checkout was being processed.",
                BookingConstants.ErrorCodes.CheckoutConcurrencyConflict);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await RollbackSafelyAsync(transaction);
            _logger.LogWarning(exception, "Checkout failed because a selected seat is no longer available.");
            return Fail(
                409,
                "One or more selected seats are no longer available.",
                BookingConstants.ErrorCodes.SeatUnavailable);
        }
    }

    private ServiceResult<CheckoutResponse>? ValidateRequest(
        CheckoutRequest request,
        IReadOnlyList<string> seatIds,
        IReadOnlyList<CheckoutFoodItemRequest> foodItems)
    {
        if (string.IsNullOrWhiteSpace(request.ShowtimeId) || seatIds.Count == 0)
        {
            return Fail(400, "Showtime and at least one seat are required.", BookingConstants.ErrorCodes.ValidationError);
        }

        if (seatIds.Count != (request.ShowtimeSeatIds?.Count ?? 0) ||
            seatIds.Count > Math.Max(1, _settings.MaxSeatsPerCheckout))
        {
            return Fail(
                400,
                "Seat selection contains duplicates or exceeds the checkout limit.",
                BookingConstants.ErrorCodes.InvalidSeatSelection);
        }

        if (foodItems.Any(item =>
                item is null ||
                string.IsNullOrWhiteSpace(item.FbItemId) ||
                item.Quantity <= 0))
        {
            return Fail(400, "Food item data is invalid.", BookingConstants.ErrorCodes.ValidationError);
        }

        var distinctFoodIds = NormalizeIds(foodItems.Select(item => item.FbItemId));
        if (distinctFoodIds.Count != foodItems.Count)
        {
            return Fail(400, "Food items must not contain duplicates.", BookingConstants.ErrorCodes.ValidationError);
        }

        return null;
    }

    private static ServiceResult<CheckoutResponse>? ValidateSeats(
        IReadOnlyCollection<ShowtimeSeat> seats,
        IReadOnlyCollection<string> requestedSeatIds,
        string showtimeId,
        string userId,
        DateTime now)
    {
        if (seats.Count != requestedSeatIds.Count)
        {
            return Fail(
                404,
                "One or more showtime seats were not found.",
                BookingConstants.ErrorCodes.ShowtimeSeatNotFound);
        }

        if (seats.Any(item => !string.Equals(item.ShowtimeId, showtimeId, StringComparison.OrdinalIgnoreCase)))
        {
            return Fail(
                400,
                "All selected seats must belong to the requested showtime.",
                BookingConstants.ErrorCodes.InvalidSeatSelection);
        }

        if (seats.Any(item => !item.Seat.IsActive || item.BookingSeat is not null))
        {
            return Fail(
                409,
                "One or more selected seats are unavailable.",
                BookingConstants.ErrorCodes.SeatUnavailable);
        }

        if (seats.Any(item =>
                !string.Equals(
                    item.SeatStatus,
                    BookingConstants.ShowtimeSeatStatus.Locked,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return Fail(
                409,
                "One or more selected seats are not locked.",
                BookingConstants.ErrorCodes.SeatUnavailable);
        }

        if (seats.Any(item =>
                !string.Equals(item.LockedByUserId, userId, StringComparison.OrdinalIgnoreCase)))
        {
            return Fail(
                409,
                "One or more selected seats are locked by another user.",
                BookingConstants.ErrorCodes.SeatNotLockedByUser);
        }

        if (seats.Any(item => item.LockedUntil is null || item.LockedUntil <= now))
        {
            return Fail(
                409,
                "One or more seat locks have expired.",
                BookingConstants.ErrorCodes.SeatLockExpired);
        }

        return null;
    }

    private async Task<ServiceResult<IReadOnlyList<CheckoutFoodItemResponse>>> LoadAndValidateFoodItemsAsync(
        string cinemaId,
        IReadOnlyList<CheckoutFoodItemRequest> requestedItems,
        CancellationToken cancellationToken)
    {
        if (requestedItems.Count == 0)
        {
            return ServiceResult<IReadOnlyList<CheckoutFoodItemResponse>>.Ok([]);
        }

        var requestedById = requestedItems.ToDictionary(
            item => item.FbItemId.Trim(),
            item => item.Quantity,
            StringComparer.OrdinalIgnoreCase);
        var requestedIds = requestedById.Keys.ToList();

        var foodData = await (
                from item in _dbContext.FbItems.AsNoTracking()
                where requestedIds.Contains(item.FbItemId)
                join inventory in _dbContext.CinemaFbInventories.AsNoTracking()
                        .Where(entry => entry.CinemaId == cinemaId)
                    on item.FbItemId equals inventory.FbItemId into inventoryGroup
                from inventory in inventoryGroup.DefaultIfEmpty()
                select new
                {
                    item.FbItemId,
                    item.ItemName,
                    item.Price,
                    item.ItemStatus,
                    InventoryQuantity = inventory == null ? (int?)null : inventory.Quantity
                })
            .ToListAsync(cancellationToken);

        if (foodData.Count != requestedIds.Count)
        {
            return ServiceResult<IReadOnlyList<CheckoutFoodItemResponse>>.Fail(
                404,
                "One or more food items were not found.",
                BookingConstants.ErrorCodes.FoodItemNotFound);
        }

        if (foodData.Any(item =>
                !string.Equals(
                    item.ItemStatus,
                    BookingConstants.ResourceStatus.Available,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return ServiceResult<IReadOnlyList<CheckoutFoodItemResponse>>.Fail(
                409,
                "One or more food items are unavailable.",
                BookingConstants.ErrorCodes.FoodItemUnavailable);
        }

        if (foodData.Any(item =>
                item.InventoryQuantity is null ||
                item.InventoryQuantity < requestedById[item.FbItemId]))
        {
            return ServiceResult<IReadOnlyList<CheckoutFoodItemResponse>>.Fail(
                409,
                "Insufficient food inventory at this cinema.",
                BookingConstants.ErrorCodes.InsufficientFoodStock);
        }

        var response = foodData
            .OrderBy(item => item.ItemName)
            .Select(item =>
            {
                var quantity = requestedById[item.FbItemId];
                return new CheckoutFoodItemResponse
                {
                    FbItemId = item.FbItemId,
                    ItemName = item.ItemName,
                    Quantity = quantity,
                    UnitPrice = item.Price,
                    Subtotal = item.Price * quantity
                };
            })
            .ToList();

        return ServiceResult<IReadOnlyList<CheckoutFoodItemResponse>>.Ok(response);
    }

    private async Task<ServiceResult<VoucherCalculation>> ValidateVoucherAsync(
        string voucherCode,
        string customerProfileId,
        decimal grossAmount,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var voucher = await _dbContext.Vouchers
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.VoucherCode == voucherCode, cancellationToken);

        if (voucher is null)
        {
            return ServiceResult<VoucherCalculation>.Fail(
                404,
                "Voucher was not found.",
                BookingConstants.ErrorCodes.VoucherNotFound);
        }

        if (!string.Equals(
                voucher.VoucherStatus,
                BookingConstants.VoucherStatus.Active,
                StringComparison.OrdinalIgnoreCase) ||
            now < voucher.StartDate ||
            now >= voucher.EndDate)
        {
            return ServiceResult<VoucherCalculation>.Fail(
                409,
                "Voucher is inactive or expired.",
                BookingConstants.ErrorCodes.VoucherExpired);
        }

        if (voucher.MinOrderAmount.HasValue && grossAmount < voucher.MinOrderAmount.Value)
        {
            return ServiceResult<VoucherCalculation>.Fail(
                400,
                "The order does not meet the voucher minimum amount.",
                BookingConstants.ErrorCodes.VoucherMinOrderNotMet);
        }

        if (voucher.UsedCount >= voucher.UsageLimit)
        {
            return ServiceResult<VoucherCalculation>.Fail(
                409,
                "Voucher usage limit has been reached.",
                BookingConstants.ErrorCodes.VoucherUsageLimitReached);
        }

        if (voucher.PerCustomerLimit.HasValue)
        {
            var confirmedUsages = await _dbContext.VoucherUsages
                .AsNoTracking()
                .CountAsync(
                    item =>
                        item.VoucherId == voucher.VoucherId &&
                        item.CustomerProfileId == customerProfileId &&
                        item.UsageStatus == BookingConstants.VoucherUsageStatus.Confirmed,
                    cancellationToken);

            if (confirmedUsages >= voucher.PerCustomerLimit.Value)
            {
                return ServiceResult<VoucherCalculation>.Fail(
                    409,
                    "Customer voucher usage limit has been reached.",
                    BookingConstants.ErrorCodes.VoucherCustomerLimitReached);
            }
        }

        decimal discount;
        if (string.Equals(
                voucher.DiscountType,
                BookingConstants.DiscountType.Amount,
                StringComparison.OrdinalIgnoreCase))
        {
            discount = voucher.DiscountValue;
        }
        else if (string.Equals(
                     voucher.DiscountType,
                     BookingConstants.DiscountType.Percent,
                     StringComparison.OrdinalIgnoreCase) &&
                 voucher.DiscountValue <= 100)
        {
            discount = decimal.Round(
                grossAmount * voucher.DiscountValue / 100m,
                2,
                MidpointRounding.AwayFromZero);
        }
        else
        {
            return ServiceResult<VoucherCalculation>.Fail(
                400,
                "Voucher discount configuration is invalid.",
                BookingConstants.ErrorCodes.ValidationError);
        }

        if (voucher.MaxDiscountAmount.HasValue)
        {
            discount = Math.Min(discount, voucher.MaxDiscountAmount.Value);
        }

        discount = Math.Min(discount, grossAmount);
        return ServiceResult<VoucherCalculation>.Ok(new VoucherCalculation(voucher, discount));
    }

    private static bool IsShowtimeBookable(Showtime showtime)
    {
        return string.Equals(
                   showtime.Status,
                   BookingConstants.ShowtimeStatus.Open,
                   StringComparison.OrdinalIgnoreCase) &&
               string.Equals(
                   showtime.Room.RoomStatus,
                   BookingConstants.ResourceStatus.Active,
                   StringComparison.OrdinalIgnoreCase) &&
               string.Equals(
                   showtime.Room.Cinema.CinemaStatus,
                   BookingConstants.ResourceStatus.Active,
                   StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(
                   showtime.Movie.MovieStatus,
                   BookingConstants.ResourceStatus.Inactive,
                   StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(
                   showtime.Movie.MovieStatus,
                   BookingConstants.ResourceStatus.Ended,
                   StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(showtime.Movie.AgeRating, "C", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> NormalizeIds(IEnumerable<string>? ids)
    {
        if (ids is null)
        {
            return [];
        }

        return ids
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException { Number: 2601 or 2627 };
    }

    private static ServiceResult<CheckoutResponse> Fail(int statusCode, string message, string errorCode)
    {
        return ServiceResult<CheckoutResponse>.Fail(statusCode, message, errorCode);
    }

    private static async Task<ServiceResult<CheckoutResponse>> RollbackAndFailAsync(
        IDbContextTransaction transaction,
        int statusCode,
        string message,
        string errorCode,
        CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);
        return Fail(statusCode, message, errorCode);
    }

    private static async Task RollbackSafelyAsync(IDbContextTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch
        {
            // Preserve the original database exception.
        }
    }

    private static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    private sealed record VoucherCalculation(Voucher Voucher, decimal DiscountAmount);
}
