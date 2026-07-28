using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.FoodAndBeverage;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CinemaSystem.Tests;

public sealed class FbItemApiIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task CreateFbItem_ThenStaffSellsAtCounter_Succeeds()
    {
        await using var factory = new CinemaWebApplicationFactory();

        // 1. Seed Cinema branch & Payment provider
        const string cinemaId = "CIN_FB_TEST_01";
        await SeedBaseDataAsync(factory, cinemaId);

        // 2. Admin creates an F&B Item via API: POST /api/fb-items
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Admin());

        var createFbRequest = new CreateFbItemRequest
        {
            ItemName = "Combo Bắp Caramel & Pepsi Large",
            Price = 85000,
            ItemStatus = "AVAILABLE"
        };

        var createResponse = await adminClient.PostAsJsonAsync("/api/fb-items", createFbRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createResult = await createResponse.Content.ReadFromJsonAsync<ApiResponse<FbItemResponse>>(JsonOptions);
        Assert.NotNull(createResult);
        Assert.True(createResult.Success);
        Assert.NotNull(createResult.Data);

        var fbItemId = createResult.Data.FbItemId;
        Assert.False(string.IsNullOrWhiteSpace(fbItemId));
        Assert.Equal("Combo Bắp Caramel & Pepsi Large", createResult.Data.ItemName);
        Assert.Equal(85000, createResult.Data.Price);

        // 3. Admin/Manager sets cinema inventory stock via API: PUT /api/fb-items/cinemas/inventory
        var updateStockRequest = new UpdateCinemaFbInventoryRequest
        {
            CinemaId = cinemaId,
            FbItemId = fbItemId,
            Quantity = 50
        };

        var stockResponse = await adminClient.PutAsJsonAsync("/api/fb-items/cinemas/inventory", updateStockRequest);
        Assert.Equal(HttpStatusCode.OK, stockResponse.StatusCode);

        var stockResult = await stockResponse.Content.ReadFromJsonAsync<ApiResponse<CinemaFbInventoryResponse>>(JsonOptions);
        Assert.NotNull(stockResult);
        Assert.True(stockResult.Success);
        Assert.Equal(50, stockResult.Data!.Quantity);

        // 4. Staff sells the F&B item at counter POS via API: POST /api/fb-items/counter-orders
        using var staffClient = factory.CreateClient();
        staffClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Staff());

        var counterOrderRequest = new CreateCounterFbOrderRequest
        {
            CinemaId = cinemaId,
            ShiftId = "SHIFT_MORNING_01",
            GuestName = "Khách Hàng Nguyễn Văn A",
            GuestPhone = "0987654321",
            Items = new List<FbOrderItemRequest>
            {
                new FbOrderItemRequest
                {
                    FbItemId = fbItemId,
                    Quantity = 2
                }
            },
            PaymentMethod = "CASH",
            ReceivedAmount = 200000
        };

        var orderResponse = await staffClient.PostAsJsonAsync("/api/fb-items/counter-orders", counterOrderRequest);
        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);

        var orderResult = await orderResponse.Content.ReadFromJsonAsync<ApiResponse<FbFulfillmentResponse>>(JsonOptions);
        Assert.NotNull(orderResult);
        Assert.True(orderResult.Success);
        Assert.NotNull(orderResult.Data);

        // Verify order calculations: 2 * 85,000 = 170,000, Received = 200,000, Change = 30,000
        Assert.Equal(170000, orderResult.Data.TotalAmount);
        Assert.Equal(200000, orderResult.Data.ReceivedAmount);
        Assert.Equal(30000, orderResult.Data.ChangeAmount);
        Assert.Equal(cinemaId, orderResult.Data.CinemaId);
        Assert.False(string.IsNullOrWhiteSpace(orderResult.Data.BookingId));

        // 5. Verify database state
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        // Stock reduced from 50 to 48
        var updatedInv = await db.CinemaFbInventories.FirstOrDefaultAsync(i => i.CinemaId == cinemaId && i.FbItemId == fbItemId);
        Assert.NotNull(updatedInv);
        Assert.Equal(48, updatedInv.Quantity);

        // Booking record created with status PAID and FULFILLED
        var booking = await db.Bookings.Include(b => b.BookingFbItems).FirstOrDefaultAsync(b => b.BookingId == orderResult.Data.BookingId);
        Assert.NotNull(booking);
        Assert.Equal("PAID", booking.BookingStatus);
        Assert.Equal("FULFILLED", booking.FbFulfillmentStatus);
        Assert.Equal(170000, booking.TotalAmount);
        Assert.Single(booking.BookingFbItems);
        Assert.Equal(fbItemId, booking.BookingFbItems.First().FbItemId);
        Assert.Equal(2, booking.BookingFbItems.First().Quantity);

        // Payment record created in DB
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.BookingId == orderResult.Data.BookingId);
        Assert.NotNull(payment);
        Assert.Equal("SUCCESS", payment.PaymentStatus);
        Assert.Equal(170000, payment.Amount);
    }

    private static async Task SeedBaseDataAsync(CinemaWebApplicationFactory factory, string cinemaId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        if (!await db.Cinemas.AnyAsync(c => c.CinemaId == cinemaId))
        {
            db.Cinemas.Add(new Cinema
            {
                CinemaId = cinemaId,
                CinemaName = "Rạp Lotte Cinema Q1",
                Address = "123 Lê Duẩn, Q1",
                City = "TP. Hồ Chí Minh",
                CinemaStatus = "ACTIVE"
            });
        }

        if (!await db.PaymentProviders.AnyAsync(p => p.PaymentProviderId == "PAYPROV_POS"))
        {
            db.PaymentProviders.Add(new PaymentProvider
            {
                PaymentProviderId = "PAYPROV_POS",
                ProviderName = "POS Counter Cash/Card",
                ProviderStatus = "ACTIVE"
            });
        }

        await db.SaveChangesAsync();
    }
}
