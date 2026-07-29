using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Tests;

public sealed class SeatTypeCatalogServiceTests
{
    [Fact]
    public async Task DeleteAsync_UnusedSeatType_DeletesCatalogEntry()
    {
        await using var dbContext = CreateDbContext();
        dbContext.SeatTypes.Add(CreateSeatType("TYPE_UNUSED", "UNUSED"));
        await dbContext.SaveChangesAsync();

        var service = new SeatTypeCatalogService(dbContext);
        var result = await service.DeleteAsync("TYPE_UNUSED", CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.Data);
        Assert.False(await dbContext.SeatTypes.AnyAsync());
    }

    [Fact]
    public async Task DeleteAsync_SeatTypeInUse_ReturnsConflictAndKeepsCatalogEntry()
    {
        await using var dbContext = CreateDbContext();
        dbContext.SeatTypes.Add(CreateSeatType("TYPE_USED", "USED"));
        dbContext.Seats.Add(new Seat
        {
            SeatId = "SEAT_1",
            RoomId = "ROOM_1",
            SeatTypeId = "TYPE_USED",
            SeatCode = "A1",
            RowLabel = "A",
            SeatNumber = 1,
            IsActive = true
        });
        await dbContext.SaveChangesAsync();

        var service = new SeatTypeCatalogService(dbContext);
        var result = await service.DeleteAsync("TYPE_USED", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("SEAT_TYPE_IN_USE", result.ErrorCode);
        Assert.True(await dbContext.SeatTypes.AnyAsync());
    }

    [Fact]
    public async Task DeleteAsync_MissingSeatType_ReturnsNotFound()
    {
        await using var dbContext = CreateDbContext();
        var service = new SeatTypeCatalogService(dbContext);

        var result = await service.DeleteAsync("TYPE_MISSING", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("SEAT_TYPE_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsGlobalUsageCount()
    {
        await using var dbContext = CreateDbContext();
        dbContext.SeatTypes.Add(CreateSeatType("TYPE_USED", "USED"));
        dbContext.Seats.Add(new Seat
        {
            SeatId = "SEAT_1",
            RoomId = "ROOM_1",
            SeatTypeId = "TYPE_USED",
            SeatCode = "A1",
            RowLabel = "A",
            SeatNumber = 1,
            IsActive = true
        });
        await dbContext.SaveChangesAsync();

        var service = new SeatTypeCatalogService(dbContext);
        var result = await service.GetAllAsync(true, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, Assert.Single(result.Data!).UsageCount);
    }

    [Fact]
    public async Task MergeAsync_CompatibleSeatTypes_ReassignsSeatsAndDeletesSource()
    {
        await using var dbContext = CreateDbContext();
        dbContext.SeatTypes.AddRange(
            CreateSeatType("TYPE_SOURCE", "SOURCE"),
            CreateSeatType("TYPE_TARGET", "TARGET"));
        dbContext.Seats.Add(new Seat
        {
            SeatId = "SEAT_1",
            RoomId = "ROOM_1",
            SeatTypeId = "TYPE_SOURCE",
            SeatCode = "A1",
            RowLabel = "A",
            SeatNumber = 1,
            IsActive = true
        });
        await dbContext.SaveChangesAsync();

        var service = new SeatTypeCatalogService(dbContext);
        var result = await service.MergeAsync(
            "TYPE_SOURCE",
            "TYPE_TARGET",
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.Data);
        Assert.False(await dbContext.SeatTypes.AnyAsync(
            item => item.SeatTypeId == "TYPE_SOURCE"));
        Assert.Equal("TYPE_TARGET", (await dbContext.Seats.SingleAsync()).SeatTypeId);
    }

    [Fact]
    public async Task MergeAsync_IncompatibleSeatTypes_ReturnsConflict()
    {
        await using var dbContext = CreateDbContext();
        var source = CreateSeatType("TYPE_SOURCE", "SOURCE");
        var target = CreateSeatType("TYPE_TARGET", "TARGET");
        target.ExtraFee = 10000;
        dbContext.SeatTypes.AddRange(source, target);
        await dbContext.SaveChangesAsync();

        var service = new SeatTypeCatalogService(dbContext);
        var result = await service.MergeAsync(
            "TYPE_SOURCE",
            "TYPE_TARGET",
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("SEAT_TYPE_MERGE_INCOMPATIBLE", result.ErrorCode);
        Assert.Equal(2, await dbContext.SeatTypes.CountAsync());
    }

    private static SeatType CreateSeatType(string id, string name) => new()
    {
        SeatTypeId = id,
        TypeName = name,
        ExtraFee = 0,
        SeatSpan = 1,
        IsActive = true,
        SortOrder = 0
    };

    private static CinemaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CinemaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new CinemaDbContext(options);
    }
}
