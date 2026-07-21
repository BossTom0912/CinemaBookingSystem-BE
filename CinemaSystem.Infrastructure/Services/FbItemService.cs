using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.FoodAndBeverage;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CinemaSystem.Infrastructure.Services;

public sealed class FbItemService : IFbItemService
{
    private readonly CinemaDbContext _dbContext;
    private readonly ILogger<FbItemService> _logger;

    public FbItemService(CinemaDbContext dbContext, ILogger<FbItemService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ServiceResult<FbItemResponse>> CreateAsync(CreateFbItemRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Price < 0)
        {
            return ServiceResult<FbItemResponse>.Fail(400, FbConstants.Messages.PriceNonNegative, FbConstants.ErrorCodes.InvalidPrice);
        }

        var itemId = $"FBI_{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var fbItem = new FbItem
        {
            FbItemId = itemId,
            ItemName = request.ItemName.Trim(),
            Price = request.Price,
            ItemStatus = string.IsNullOrWhiteSpace(request.ItemStatus) ? FbConstants.ItemStatus.Available : request.ItemStatus.Trim().ToUpperInvariant()
        };

        _dbContext.FbItems.Add(fbItem);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = MapToResponse(fbItem);
        return ServiceResult<FbItemResponse>.Ok(response, FbConstants.Messages.ItemCreatedSuccess, 201);
    }

    public async Task<ServiceResult<IReadOnlyList<FbItemResponse>>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        var items = await _dbContext.FbItems
            .AsNoTracking()
            .Where(x => x.ItemStatus == FbConstants.ItemStatus.Available)
            .Select(x => new FbItemResponse
            {
                FbItemId = x.FbItemId,
                ItemName = x.ItemName,
                Price = x.Price,
                ItemStatus = x.ItemStatus
            })
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<FbItemResponse>>.Ok(items);
    }

    public async Task<ServiceResult<IReadOnlyList<FbItemResponse>>> GetAllForAdminAsync(CancellationToken cancellationToken = default)
    {
        var items = await _dbContext.FbItems
            .AsNoTracking()
            .Select(x => new FbItemResponse
            {
                FbItemId = x.FbItemId,
                ItemName = x.ItemName,
                Price = x.Price,
                ItemStatus = x.ItemStatus
            })
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<FbItemResponse>>.Ok(items);
    }

    public async Task<ServiceResult<FbItemResponse>> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var fbItem = await _dbContext.FbItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.FbItemId == id, cancellationToken);

        if (fbItem == null)
        {
            return ServiceResult<FbItemResponse>.Fail(404, FbConstants.Messages.ItemNotFound, FbConstants.ErrorCodes.NotFound);
        }

        return ServiceResult<FbItemResponse>.Ok(MapToResponse(fbItem));
    }

    public async Task<ServiceResult<FbItemResponse>> UpdateAsync(string id, UpdateFbItemRequest request, CancellationToken cancellationToken = default)
    {
        var fbItem = await _dbContext.FbItems
            .FirstOrDefaultAsync(x => x.FbItemId == id, cancellationToken);

        if (fbItem == null)
        {
            return ServiceResult<FbItemResponse>.Fail(404, FbConstants.Messages.ItemNotFound, FbConstants.ErrorCodes.NotFound);
        }

        if (request.Price < 0)
        {
            return ServiceResult<FbItemResponse>.Fail(400, FbConstants.Messages.PriceNonNegative, FbConstants.ErrorCodes.InvalidPrice);
        }

        fbItem.ItemName = request.ItemName.Trim();
        fbItem.Price = request.Price;
        fbItem.ItemStatus = request.ItemStatus.Trim().ToUpperInvariant();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<FbItemResponse>.Ok(MapToResponse(fbItem), FbConstants.Messages.ItemUpdatedSuccess);
    }

    public async Task<ServiceResult<bool>> DeactivateAsync(string id, CancellationToken cancellationToken = default)
    {
        var fbItem = await _dbContext.FbItems
            .FirstOrDefaultAsync(x => x.FbItemId == id, cancellationToken);

        if (fbItem == null)
        {
            return ServiceResult<bool>.Fail(404, FbConstants.Messages.ItemNotFound, FbConstants.ErrorCodes.NotFound);
        }

        fbItem.ItemStatus = FbConstants.ItemStatus.Inactive;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true, FbConstants.Messages.ItemDeactivatedSuccess);
    }

    public async Task<ServiceResult<IReadOnlyList<CinemaFbInventoryResponse>>> GetCinemaInventoryAsync(string cinemaId, CancellationToken cancellationToken = default)
    {
        var inventory = await _dbContext.CinemaFbInventories
            .AsNoTracking()
            .Where(x => x.CinemaId == cinemaId)
            .Include(x => x.FbItem)
            .Select(x => new CinemaFbInventoryResponse
            {
                CinemaInventoryId = x.CinemaInventoryId,
                CinemaId = x.CinemaId,
                FbItemId = x.FbItemId,
                ItemName = x.FbItem.ItemName,
                Price = x.FbItem.Price,
                Quantity = x.Quantity
            })
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<CinemaFbInventoryResponse>>.Ok(inventory);
    }

    public async Task<ServiceResult<CinemaFbInventoryResponse>> UpdateCinemaInventoryAsync(
        UpdateCinemaFbInventoryRequest request,
        string? currentStaffCinemaId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        // Scoped Authorization Check: Branch staff/manager can only edit their assigned cinema
        if (!isSystemAdmin && (string.IsNullOrEmpty(currentStaffCinemaId) || !string.Equals(currentStaffCinemaId, request.CinemaId, StringComparison.OrdinalIgnoreCase)))
        {
            return ServiceResult<CinemaFbInventoryResponse>.Fail(
                403,
                FbConstants.Messages.ForbiddenBranchInventory,
                FbConstants.ErrorCodes.ForbiddenCinemaBranch);
        }

        var fbItemExists = await _dbContext.FbItems.AnyAsync(x => x.FbItemId == request.FbItemId, cancellationToken);
        if (!fbItemExists)
        {
            return ServiceResult<CinemaFbInventoryResponse>.Fail(404, FbConstants.Messages.ItemNotFound, FbConstants.ErrorCodes.NotFound);
        }

        var cinemaExists = await _dbContext.Cinemas.AnyAsync(x => x.CinemaId == request.CinemaId, cancellationToken);
        if (!cinemaExists)
        {
            return ServiceResult<CinemaFbInventoryResponse>.Fail(404, FbConstants.Messages.CinemaBranchNotFound, FbConstants.ErrorCodes.NotFound);
        }

        var inv = await _dbContext.CinemaFbInventories
            .Include(x => x.FbItem)
            .FirstOrDefaultAsync(x => x.CinemaId == request.CinemaId && x.FbItemId == request.FbItemId, cancellationToken);

        if (inv == null)
        {
            inv = new CinemaFbInventory
            {
                CinemaInventoryId = $"INV_{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
                CinemaId = request.CinemaId,
                FbItemId = request.FbItemId,
                Quantity = request.Quantity
            };
            _dbContext.CinemaFbInventories.Add(inv);
        }
        else
        {
            inv.Quantity = request.Quantity;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new CinemaFbInventoryResponse
        {
            CinemaInventoryId = inv.CinemaInventoryId,
            CinemaId = inv.CinemaId,
            FbItemId = inv.FbItemId,
            ItemName = inv.FbItem?.ItemName ?? string.Empty,
            Price = inv.FbItem?.Price ?? 0,
            Quantity = inv.Quantity
        };

        return ServiceResult<CinemaFbInventoryResponse>.Ok(response, FbConstants.Messages.InventoryUpdatedSuccess);
    }

    public async Task<ServiceResult<FbFulfillmentResponse>> CreateCounterOrderAsync(
        CreateCounterFbOrderRequest request,
        string staffProfileId,
        string currentStaffCinemaId,
        CancellationToken cancellationToken = default)
    {
        if (request.Items == null || !request.Items.Any())
        {
            return ServiceResult<FbFulfillmentResponse>.Fail(400, FbConstants.Messages.EmptyOrderError, FbConstants.ErrorCodes.EmptyOrder);
        }

        // Validate cinema scoping
        var cinemaId = request.CinemaId;
        if (!string.IsNullOrEmpty(currentStaffCinemaId) && !string.Equals(cinemaId, currentStaffCinemaId, StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<FbFulfillmentResponse>.Fail(403, FbConstants.Messages.ForbiddenBranchOrder, FbConstants.ErrorCodes.ForbiddenCinemaBranch);
        }

        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    decimal grossAmount = 0;
                    var bookingFbItems = new List<BookingFbItem>();
                    var responseItems = new List<CounterFbOrderItemResponse>();

                    foreach (var itemReq in request.Items)
                    {
                        var itemId = itemReq.FbItemId;
                        var fbItem = await _dbContext.FbItems.FirstOrDefaultAsync(x => x.FbItemId == itemId, cancellationToken);
                        if (fbItem == null || fbItem.ItemStatus == FbConstants.ItemStatus.Inactive)
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            return ServiceResult<FbFulfillmentResponse>.Fail(404, $"F&B item '{itemId}' is unavailable.", FbConstants.ErrorCodes.ItemUnavailable);
                        }

                        // Pure SQL Atomic Update to prevent concurrency overselling
                        int rowsAffected = await _dbContext.Database.ExecuteSqlRawAsync(
                            "UPDATE CINEMA_FB_INVENTORY SET quantity = quantity - {0} WHERE cinemaId = {1} AND fbItemId = {2} AND quantity >= {3}",
                            new object[] { itemReq.Quantity, cinemaId, itemId, itemReq.Quantity },
                            cancellationToken);

                        if (rowsAffected == 0)
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            return ServiceResult<FbFulfillmentResponse>.Fail(
                                409,
                                $"Insufficient stock for item '{fbItem.ItemName}' at this cinema branch.",
                                FbConstants.ErrorCodes.InsufficientStock);
                        }

                        // Support Modifiers/Toppings/Flavors extra fees
                        decimal optionsExtraFee = itemReq.Options?.Sum(opt => opt.ExtraFee) ?? 0;
                        decimal unitPrice = itemReq.UnitPrice ?? (fbItem.Price + optionsExtraFee);
                        decimal subtotal = unitPrice * itemReq.Quantity;
                        grossAmount += subtotal;

                        bookingFbItems.Add(new BookingFbItem
                        {
                            BookingFbitemId = $"BFI_{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
                            FbItemId = itemId,
                            Quantity = itemReq.Quantity,
                            UnitPrice = unitPrice,
                            Subtotal = subtotal
                        });

                        responseItems.Add(new CounterFbOrderItemResponse
                        {
                            FbItemId = itemId,
                            ItemName = fbItem.ItemName,
                            Quantity = itemReq.Quantity,
                            UnitPrice = fbItem.Price,
                            ExtraFee = optionsExtraFee,
                            Subtotal = subtotal,
                            Options = itemReq.Options ?? new()
                        });
                    }

                    // Calculate discount and total amount
                    decimal discountAmount = request.DiscountAmount ?? 0;
                    decimal totalAmount = request.TotalAmount ?? Math.Max(0, grossAmount - discountAmount);

                    // Payment Method & Cash change calculation
                    var paymentMethod = string.IsNullOrWhiteSpace(request.PaymentMethod) ? "CASH" : request.PaymentMethod.Trim().ToUpperInvariant();
                    decimal? receivedAmount = request.ReceivedAmount;
                    decimal? changeAmount = request.ChangeAmount ?? (receivedAmount.HasValue ? Math.Max(0, receivedAmount.Value - totalAmount) : null);

                    var now = DateTime.UtcNow;
                    string targetBookingId;

                    // Option A: Attach to existing Booking if BookingId is provided
                    Booking? existingBooking = null;
                    if (!string.IsNullOrWhiteSpace(request.BookingId))
                    {
                        existingBooking = await _dbContext.Bookings
                            .Include(b => b.BookingFbItems)
                            .FirstOrDefaultAsync(b => b.BookingId == request.BookingId, cancellationToken);
                    }

                    if (existingBooking != null)
                    {
                        targetBookingId = existingBooking.BookingId;
                        foreach (var bfi in bookingFbItems)
                        {
                            bfi.BookingId = targetBookingId;
                            _dbContext.BookingFbItems.Add(bfi);
                        }

                        existingBooking.TotalAmount += totalAmount;
                        existingBooking.FbFulfillmentStatus = FbConstants.FulfillmentStatus.Fulfilled;
                        existingBooking.FbFulfilledAt = now;
                        existingBooking.FbFulfilledByStaffProfileId = staffProfileId;
                    }
                    else
                    {
                        targetBookingId = !string.IsNullOrWhiteSpace(request.BookingId)
                            ? request.BookingId
                            : $"BKG_{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

                        var booking = new Booking
                        {
                            BookingId = targetBookingId,
                            ShowtimeId = request.ShowtimeId,
                            CustomerProfileId = request.CustomerProfileId,
                            CreatedByStaffProfileId = staffProfileId,
                            BookingChannel = FbConstants.Channel.Counter,
                            GuestName = request.GuestName,
                            GuestPhone = request.GuestPhone,
                            GuestEmail = request.GuestEmail,
                            BookingStatus = DomainConstants.EntityStatus.Paid,
                            TotalAmount = totalAmount,
                            FbFulfillmentStatus = FbConstants.FulfillmentStatus.Fulfilled,
                            FbFulfilledAt = now,
                            FbFulfilledByStaffProfileId = staffProfileId,
                            CreatedAt = now,
                            BookingFbItems = bookingFbItems
                        };

                        _dbContext.Bookings.Add(booking);
                    }

                    // Record Payment entry
                    var paymentProvider = await _dbContext.PaymentProviders
                        .Select(p => p.PaymentProviderId)
                        .FirstOrDefaultAsync(cancellationToken) ?? "PAYPROV_POS";

                    var payment = new Payment
                    {
                        PaymentId = $"PAY_{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
                        BookingId = targetBookingId,
                        PaymentProviderId = paymentProvider,
                        Amount = totalAmount,
                        PaymentStatus = DomainConstants.PaymentStatus.Success,
                        PaidAt = now,
                        PaymentMethod = paymentMethod,
                        TransactionCode = $"POS_{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    _dbContext.Payments.Add(payment);

                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    var response = new FbFulfillmentResponse
                    {
                        BookingId = targetBookingId,
                        CinemaId = cinemaId,
                        ShiftId = request.ShiftId,
                        CustomerProfileId = request.CustomerProfileId,
                        GuestName = request.GuestName,
                        GuestPhone = request.GuestPhone,
                        GrossAmount = grossAmount,
                        DiscountAmount = discountAmount,
                        VoucherCode = request.VoucherCode,
                        TotalAmount = totalAmount,
                        PaymentMethod = paymentMethod,
                        ReceivedAmount = receivedAmount,
                        ChangeAmount = changeAmount,
                        FbFulfillmentStatus = FbConstants.FulfillmentStatus.Fulfilled,
                        FbFulfilledAt = now,
                        StaffProfileId = staffProfileId,
                        Items = responseItems,
                        Message = FbConstants.Messages.OrderPlacedSuccess
                    };

                    return ServiceResult<FbFulfillmentResponse>.Ok(response, FbConstants.Messages.OrderCompleted, 201);
                }
                catch
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    throw;
                }
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to process counter F&B order for cinema {CinemaId} by staff profile {StaffProfileId}",
                cinemaId,
                staffProfileId);
            return ServiceResult<FbFulfillmentResponse>.Fail(
                500,
                FbConstants.Messages.CounterOrderProcessingFailed,
                FbConstants.ErrorCodes.InternalError);
        }
    }

    public async Task<ServiceResult<FbFulfillmentResponse>> FulfillOrderAsync(
        string bookingId,
        string staffProfileId,
        string currentStaffCinemaId,
        CancellationToken cancellationToken = default)
    {
        var booking = await _dbContext.Bookings
            .Include(x => x.BookingFbItems)
            .Include(x => x.Showtime)
            .ThenInclude(s => s!.Room)
            .FirstOrDefaultAsync(x => x.BookingId == bookingId, cancellationToken);

        if (booking == null)
        {
            return ServiceResult<FbFulfillmentResponse>.Fail(404, FbConstants.Messages.BookingNotFound, FbConstants.ErrorCodes.NotFound);
        }

        // Cross-Branch Scan Protection: Resolve cinema scope from database if missing in claims
        var resolvedStaffCinemaId = currentStaffCinemaId;
        if (string.IsNullOrEmpty(resolvedStaffCinemaId))
        {
            var staffProfile = await _dbContext.StaffProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(profile =>
                    (profile.StaffProfileId == staffProfileId || profile.UserId == staffProfileId)
                    && profile.EmploymentStatus == BookingConstants.ResourceStatus.Active, cancellationToken);
            if (staffProfile != null)
            {
                resolvedStaffCinemaId = staffProfile.CinemaId;
            }
        }

        var bookingCinemaId = booking.Showtime?.Room?.CinemaId;
        if (!string.IsNullOrEmpty(bookingCinemaId) &&
            !string.IsNullOrEmpty(resolvedStaffCinemaId) &&
            !string.Equals(bookingCinemaId, resolvedStaffCinemaId, StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<FbFulfillmentResponse>.Fail(
                403,
                $"F&B order belongs to cinema branch '{bookingCinemaId}'. Cannot fulfill at branch '{resolvedStaffCinemaId}'.",
                FbConstants.ErrorCodes.WrongCinemaBranch);
        }

        if (booking.FbFulfillmentStatus == FbConstants.FulfillmentStatus.NotRequired || !booking.BookingFbItems.Any())
        {
            return ServiceResult<FbFulfillmentResponse>.Fail(400, FbConstants.Messages.NoFbItemsError, FbConstants.ErrorCodes.NoFbItems);
        }

        // Anti-Fraud Control Check
        if (booking.FbFulfillmentStatus == FbConstants.FulfillmentStatus.Fulfilled)
        {
            var fulfilledTimeStr = booking.FbFulfilledAt.HasValue
                ? booking.FbFulfilledAt.Value.ToString("u")
                : "earlier";

            return ServiceResult<FbFulfillmentResponse>.Fail(
                400,
                $"ALERT: F&B items were ALREADY CLAIMED on {fulfilledTimeStr}.",
                FbConstants.ErrorCodes.AlreadyFulfilled);
        }

        if (booking.FbFulfillmentStatus == FbConstants.FulfillmentStatus.Cancelled)
        {
            return ServiceResult<FbFulfillmentResponse>.Fail(400, FbConstants.Messages.BookingCancelledError, FbConstants.ErrorCodes.BookingCancelled);
        }

        booking.FbFulfillmentStatus = FbConstants.FulfillmentStatus.Fulfilled;
        booking.FbFulfilledAt = DateTime.UtcNow;
        booking.FbFulfilledByStaffProfileId = staffProfileId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new FbFulfillmentResponse
        {
            BookingId = booking.BookingId,
            FbFulfillmentStatus = booking.FbFulfillmentStatus,
            FbFulfilledAt = booking.FbFulfilledAt,
            StaffProfileId = staffProfileId,
            Message = FbConstants.Messages.FulfillmentCompleted
        };

        return ServiceResult<FbFulfillmentResponse>.Ok(response, FbConstants.Messages.FulfillmentCompleted);
    }

    public async Task<ServiceResult<bool>> RestockCanceledOrderAsync(string bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await _dbContext.Bookings
            .Include(x => x.BookingFbItems)
            .Include(x => x.Showtime)
            .ThenInclude(s => s!.Room)
            .FirstOrDefaultAsync(x => x.BookingId == bookingId, cancellationToken);

        if (booking == null || !booking.BookingFbItems.Any())
        {
            return ServiceResult<bool>.Ok(false, "No F&B items found for restock.");
        }

        // Food Safety Rule: Items already prepared/given to customer CANNOT be returned to inventory
        if (booking.FbFulfillmentStatus == FbConstants.FulfillmentStatus.Fulfilled)
        {
            return ServiceResult<bool>.Fail(400, FbConstants.Messages.FoodSafetyRestockDenied, FbConstants.ErrorCodes.FoodSafetyRestockDenied);
        }

        if (booking.FbFulfillmentStatus != FbConstants.FulfillmentStatus.Pending)
        {
            return ServiceResult<bool>.Ok(false, "No pending F&B restock required.");
        }

        var cinemaId = booking.Showtime?.Room?.CinemaId;
        if (string.IsNullOrEmpty(cinemaId))
        {
            return ServiceResult<bool>.Ok(false, "Cinema ID could not be determined for restock.");
        }

        foreach (var item in booking.BookingFbItems)
        {
            await _dbContext.Database.ExecuteSqlRawAsync(
                "UPDATE CINEMA_FB_INVENTORY SET quantity = quantity + {0} WHERE cinemaId = {1} AND fbItemId = {2}",
                new object[] { item.Quantity, cinemaId, item.FbItemId },
                cancellationToken);
        }

        booking.FbFulfillmentStatus = FbConstants.FulfillmentStatus.Cancelled;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true, FbConstants.Messages.RestockSuccess);
    }

    private static FbItemResponse MapToResponse(FbItem fbItem)
    {
        return new FbItemResponse
        {
            FbItemId = fbItem.FbItemId,
            ItemName = fbItem.ItemName,
            Price = fbItem.Price,
            ItemStatus = fbItem.ItemStatus
        };
    }
}
