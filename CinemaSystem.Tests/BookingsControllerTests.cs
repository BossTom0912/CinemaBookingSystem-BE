using System.Security.Claims;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Bookings;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Tests;

public sealed class BookingsControllerTests
{
    [Fact]
    public async Task Checkout_WithoutUserClaim_ReturnsUnauthorizedApiResponse()
    {
        var service = new FakeCheckoutService();
        var controller = CreateController(service);

        var result = await controller.Checkout(
            new CheckoutRequest { ShowtimeId = "SHO_001", ShowtimeSeatIds = ["STS_001"] },
            CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(unauthorized.Value);
        Assert.False(response.Success);
        Assert.Equal(BookingConstants.ErrorCodes.Unauthorized, response.ErrorCode);
        Assert.Null(service.CapturedUserId);
    }

    [Fact]
    public async Task Checkout_WithUserClaim_ReturnsCreatedApiResponse()
    {
        var checkoutResponse = new CheckoutResponse
        {
            BookingId = "BKG_001",
            BookingStatus = BookingConstants.BookingStatus.PendingPayment,
            ShowtimeId = "SHO_001",
            TotalAmount = 100000m,
            ExpiredAt = DateTime.UtcNow.AddMinutes(5)
        };
        var service = new FakeCheckoutService
        {
            NextResult = ServiceResult<CheckoutResponse>.Ok(
                checkoutResponse,
                "Checkout created successfully.",
                201)
        };
        var controller = CreateController(service, userId: "USR_CUSTOMER");

        var result = await controller.Checkout(
            new CheckoutRequest { ShowtimeId = "SHO_001", ShowtimeSeatIds = ["STS_001"] },
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        var response = Assert.IsType<ApiResponse<CheckoutResponse>>(objectResult.Value);
        Assert.Equal(201, objectResult.StatusCode);
        Assert.True(response.Success);
        Assert.Equal("USR_CUSTOMER", service.CapturedUserId);
        Assert.Equal("BKG_001", response.Data!.BookingId);
    }

    [Fact]
    public async Task Checkout_WhenServiceFails_ReturnsServiceStatusAndErrorCode()
    {
        var service = new FakeCheckoutService
        {
            NextResult = ServiceResult<CheckoutResponse>.Fail(
                409,
                "One or more selected seats are unavailable.",
                BookingConstants.ErrorCodes.SeatUnavailable)
        };
        var controller = CreateController(service, userId: "USR_CUSTOMER");

        var result = await controller.Checkout(
            new CheckoutRequest { ShowtimeId = "SHO_001", ShowtimeSeatIds = ["STS_001"] },
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        var response = Assert.IsType<ApiResponse<CheckoutResponse>>(objectResult.Value);
        Assert.Equal(409, objectResult.StatusCode);
        Assert.False(response.Success);
        Assert.Equal(BookingConstants.ErrorCodes.SeatUnavailable, response.ErrorCode);
        Assert.Equal("USR_CUSTOMER", service.CapturedUserId);
    }

    private static BookingsController CreateController(
        FakeCheckoutService checkoutService,
        string? userId = null)
    {
        var controller = new BookingsController(checkoutService);
        var claims = userId is null
            ? []
            : new[] { new Claim("userId", userId) };

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };

        return controller;
    }

    private sealed class FakeCheckoutService : ICheckoutService
    {
        public ServiceResult<CheckoutResponse> NextResult { get; init; } =
            ServiceResult<CheckoutResponse>.Fail(
                500,
                "Unexpected test service call.",
                BookingConstants.ErrorCodes.ValidationError);

        public string? CapturedUserId { get; private set; }

        public Task<ServiceResult<CheckoutResponse>> CheckoutAsync(
            string userId,
            CheckoutRequest request,
            CancellationToken cancellationToken)
        {
            CapturedUserId = userId;
            return Task.FromResult(NextResult);
        }
    }
}
