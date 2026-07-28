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
