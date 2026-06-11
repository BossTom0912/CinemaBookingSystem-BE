using CinemaSystem.Infrastructure.Cinemas;
using CinemaSystem.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Tests;

/// <summary>
/// Unit test cho CinemaSystem.Infrastructure/Cinemas/CinemaService.cs — GET danh sách rạp.
/// </summary>
public sealed class CinemaServiceTests
{
  [Fact]
  public async Task GetCinemasAsync_ReturnsOrderedActiveCinemas()
  {
    // Luồng: seed 2 rạp → gọi GetCinemasAsync → trả danh sách sắp xếp theo tên.
    var dbContext = CreateDbContext();
    dbContext.Cinemas.AddRange(
      new Cinema
      {
        CinemaId = "CIN_B",
        CinemaName = "Beta Cinema",
        Address = "2 Street",
        City = "HCM",
        CinemaStatus = "ACTIVE"
      },
      new Cinema
      {
        CinemaId = "CIN_A",
        CinemaName = "Alpha Cinema",
        Address = "1 Street",
        City = "HCM",
        CinemaStatus = "ACTIVE"
      });
    await dbContext.SaveChangesAsync();

    var service = new CinemaService(dbContext);
    var result = await service.GetCinemasAsync(CancellationToken.None);

    Assert.True(result.Success);
    Assert.Equal(2, result.Data!.Count);
    Assert.Equal("Alpha Cinema", result.Data[0].CinemaName);
    Assert.Equal("Beta Cinema", result.Data[1].CinemaName);
  }

  [Fact]
  public async Task GetCinemasAsync_EmptyDatabase_ReturnsEmptyList()
  {
    // Luồng: DB trống → trả list rỗng, không lỗi.
    var service = new CinemaService(CreateDbContext());
    var result = await service.GetCinemasAsync(CancellationToken.None);

    Assert.True(result.Success);
    Assert.Empty(result.Data!);
  }

  private static CinemaDbContext CreateDbContext()
  {
    var options = new DbContextOptionsBuilder<CinemaDbContext>()
      .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
      .Options;
    return new CinemaDbContext(options);
  }
}
