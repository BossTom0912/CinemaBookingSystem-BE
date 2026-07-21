using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Contracts.FoodAndBeverage;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CinemaSystem.Tests;

public sealed class FbItemServiceTests
{
    private static CinemaDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<CinemaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString("N"))
            .Options;

        return new CinemaDbContext(options);
    }

    private static FbItemService CreateService(CinemaDbContext dbContext)
    {
        return new FbItemService(dbContext, NullLogger<FbItemService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_ValidItem_ReturnsCreatedResponse()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = CreateService(dbContext);

        var request = new CreateFbItemRequest
        {
            ItemName = "Bắp Ngọt Vừa",
            Price = 45000,
            ItemStatus = "AVAILABLE"
        };

        var result = await service.CreateAsync(request);

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("Bắp Ngọt Vừa", result.Data.ItemName);
        Assert.Equal(45000, result.Data.Price);
    }

    [Fact]
    public async Task CreateAsync_NegativePrice_ReturnsBadRequest()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = CreateService(dbContext);

        var request = new CreateFbItemRequest
        {
            ItemName = "Bắp Lỗi",
            Price = -10000
        };

        var result = await service.CreateAsync(request);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task GetAllActiveAsync_ReturnsOnlyAvailableItems()
    {
        using var dbContext = CreateInMemoryDbContext();
        dbContext.FbItems.AddRange(
            new FbItem { FbItemId = "FBI_1", ItemName = "Bắp Phô Mai", Price = 55000, ItemStatus = "AVAILABLE" },
            new FbItem { FbItemId = "FBI_2", ItemName = "Nước Cốc Ngừng Bán", Price = 30000, ItemStatus = "INACTIVE" }
        );
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetAllActiveAsync();

        Assert.True(result.Success);
        Assert.Single(result.Data);
        Assert.Equal("Bắp Phô Mai", result.Data[0].ItemName);
    }

    [Fact]
    public async Task CreateCounterOrderAsync_ValidOrder_DeductsStockAndReturnsSuccess()
    {
        using var dbContext = CreateInMemoryDbContext();

        // Seed Cinema & Inventory
        dbContext.Cinemas.Add(new Cinema { CinemaId = "CIN_01", CinemaName = "Rạp 1", Address = "Q1", City = "HCM", CinemaStatus = "ACTIVE" });
        dbContext.FbItems.Add(new FbItem { FbItemId = "FBI_POPCORN", ItemName = "Bắp Caramel", Price = 50000, ItemStatus = "AVAILABLE" });
        dbContext.CinemaFbInventories.Add(new CinemaFbInventory
        {
            CinemaInventoryId = "INV_01",
            CinemaId = "CIN_01",
            FbItemId = "FBI_POPCORN",
            Quantity = 10
        });
        dbContext.PaymentProviders.Add(new PaymentProvider { PaymentProviderId = "PAYPROV_POS", ProviderName = "POS", ProviderStatus = "ACTIVE" });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var request = new CreateCounterFbOrderRequest
        {
            CinemaId = "CIN_01",
            ShiftId = "SHIFT_CA1",
            GuestName = "Nguyen Van A",
            GuestPhone = "0901234567",
            Items = new List<FbOrderItemRequest>
            {
                new FbOrderItemRequest
                {
                    FbItemId = "FBI_POPCORN",
                    Quantity = 2,
                    Options = new List<FbItemOptionRequest>
                    {
                        new FbItemOptionRequest { OptionId = "EXTRA_CARAMEL", OptionName = "Thêm Caramel", ExtraFee = 10000 }
                    }
                }
            },
            PaymentMethod = "CASH",
            ReceivedAmount = 200000
        };

        var result = await service.CreateCounterOrderAsync(request, "STAFF_01", "CIN_01");

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("CIN_01", result.Data.CinemaId);
        Assert.Equal("SHIFT_CA1", result.Data.ShiftId);
        Assert.Equal(120000, result.Data.TotalAmount); // (50k + 10k) * 2
        Assert.Equal(200000, result.Data.ReceivedAmount);
        Assert.Equal(80000, result.Data.ChangeAmount); // 200k - 120k

        // Check Inventory updated
        var inv = await dbContext.CinemaFbInventories.FirstAsync(x => x.CinemaInventoryId == "INV_01");
        Assert.Equal(8, inv.Quantity);

        // Check Payment recorded
        var payment = await dbContext.Payments.FirstOrDefaultAsync(p => p.BookingId == result.Data.BookingId);
        Assert.NotNull(payment);
        Assert.Equal(120000, payment.Amount);
        Assert.Equal("SUCCESS", payment.PaymentStatus);
    }

    [Fact]
    public async Task CreateCounterOrderAsync_AttachToExistingBooking_AppendsItemsToBooking()
    {
        using var dbContext = CreateInMemoryDbContext();

        dbContext.Cinemas.Add(new Cinema { CinemaId = "CIN_01", CinemaName = "Rạp 1", Address = "Q1", City = "HCM", CinemaStatus = "ACTIVE" });
        dbContext.FbItems.Add(new FbItem { FbItemId = "FBI_PEPSI", ItemName = "Pepsi", Price = 30000, ItemStatus = "AVAILABLE" });
        dbContext.CinemaFbInventories.Add(new CinemaFbInventory
        {
            CinemaInventoryId = "INV_02",
            CinemaId = "CIN_01",
            FbItemId = "FBI_PEPSI",
            Quantity = 20
        });
        dbContext.Bookings.Add(new Booking
        {
            BookingId = "BKG_EXISTING_100",
            BookingStatus = "PAID",
            TotalAmount = 100000,
            BookingChannel = "ONLINE",
            CreatedAt = DateTime.UtcNow
        });
        dbContext.PaymentProviders.Add(new PaymentProvider { PaymentProviderId = "PAYPROV_POS", ProviderName = "POS", ProviderStatus = "ACTIVE" });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var request = new CreateCounterFbOrderRequest
        {
            CinemaId = "CIN_01",
            BookingId = "BKG_EXISTING_100",
            Items = new List<FbOrderItemRequest>
            {
                new FbOrderItemRequest { FbItemId = "FBI_PEPSI", Quantity = 1 }
            },
            PaymentMethod = "CASH",
            ReceivedAmount = 50000
        };

        var result = await service.CreateCounterOrderAsync(request, "STAFF_01", "CIN_01");

        Assert.True(result.Success);
        Assert.Equal("BKG_EXISTING_100", result.Data.BookingId);
        Assert.Equal(30000, result.Data.TotalAmount);

        // Verify total amount of existing booking updated to 130,000 (100k original + 30k F&B)
        var updatedBooking = await dbContext.Bookings.FirstAsync(b => b.BookingId == "BKG_EXISTING_100");
        Assert.Equal(130000, updatedBooking.TotalAmount);
        Assert.Equal("FULFILLED", updatedBooking.FbFulfillmentStatus);
    }
}
