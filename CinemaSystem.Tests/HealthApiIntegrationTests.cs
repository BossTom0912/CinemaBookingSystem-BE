using System.Net;
using CinemaSystem.Tests.Infrastructure;

namespace CinemaSystem.Tests;

/// <summary>
/// Integration test cho api/health — endpoint kiểm tra sống của service.
/// </summary>
public sealed class HealthApiIntegrationTests
{
  [Fact]
  public async Task GetHealth_ReturnsOkStatus()
  {
    // Luồng: GET /api/health không cần auth → 200 + status OK.
    await using var factory = new CinemaWebApplicationFactory();
    using var client = factory.CreateClient();

    var response = await client.GetAsync("/api/health");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var json = await response.Content.ReadAsStringAsync();
    Assert.Contains("OK", json, StringComparison.OrdinalIgnoreCase);
  }
}
