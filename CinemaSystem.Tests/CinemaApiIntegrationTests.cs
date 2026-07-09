using System.Net;
using System.Text.Json;
using CinemaSystem.Contracts.Cinemas;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Infrastructure.Persistence;

using CinemaSystem.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystem.Tests;

/// <summary>
/// Integration test HTTP cho api/cinemas — GET danh sách rạp (AllowAnonymous).
/// </summary>
public sealed class CinemaApiIntegrationTests
{
  private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

  [Fact]
  public async Task GetCinemas_ReturnsSeededCinemasWithoutAuth()
  {
    // Luồng: seed rạp → GET /api/cinemas không cần token → 200 + danh sách.
    await using var factory = new CinemaWebApplicationFactory();
    await SeedCinemaAsync(factory);

    using var client = factory.CreateClient();
    var response = await client.GetAsync("/api/cinemas");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = JsonSerializer.Deserialize<ApiResponse<List<CinemaResponse>>>(
      await response.Content.ReadAsStringAsync(),
      JsonOptions);
    Assert.True(body!.Success);
    Assert.Single(body.Data!);
    Assert.Equal("E2E Cinema", body.Data![0].CinemaName);
  }

  private static async Task SeedCinemaAsync(CinemaWebApplicationFactory factory)
  {
    await using var scope = factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
    db.Cinemas.Add(new Cinema
    {
      CinemaId = "CIN_E2E",
      CinemaName = "E2E Cinema",
      Address = "1 Street",
      City = "HCM",
      CinemaStatus = "ACTIVE"
    });
    await db.SaveChangesAsync();
  }
}
