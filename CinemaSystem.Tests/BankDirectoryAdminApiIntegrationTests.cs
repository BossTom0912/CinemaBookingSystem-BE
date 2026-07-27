using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Refunds;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystem.Tests;

public sealed class BankDirectoryAdminApiIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task Upsert_AdminCreatesAndDisablesBank_CustomerOnlySeesActiveRows()
    {
        await using var factory = new CinemaWebApplicationFactory();
        using var adminClient = CreateClient(factory, TestAuthTokens.Admin());

        var createResponse = await adminClient.PutAsJsonAsync(
            "/api/admin/banks/configured_bank",
            Request(isActive: true));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await DeserializeAsync<ApiResponse<BankDirectoryResponse>>(createResponse);
        Assert.True(created!.Success);
        Assert.Equal("CONFIGURED_BANK", created.Data!.BankCode);

        var directoryResponse = await adminClient.GetAsync("/api/admin/banks");
        var directory = await DeserializeAsync<ApiResponse<List<BankDirectoryResponse>>>(directoryResponse);
        Assert.Equal(HttpStatusCode.OK, directoryResponse.StatusCode);
        Assert.Single(directory!.Data!);

        var updateResponse = await adminClient.PutAsJsonAsync(
            "/api/admin/banks/configured_bank",
            Request(isActive: false));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var customerClient = CreateClient(factory, TestAuthTokens.Customer());
        var customerResponse = await customerClient.GetAsync("/api/customer/banks");
        var customerBody = await DeserializeAsync<ApiResponse<List<BankResponse>>>(customerResponse);

        Assert.Equal(HttpStatusCode.OK, customerResponse.StatusCode);
        Assert.Empty(customerBody!.Data!);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var stored = await db.BankDirectories.SingleAsync();
        Assert.False(stored.IsActive);
        Assert.NotNull(stored.UpdatedAt);
    }

    [Fact]
    public async Task Upsert_ManagerToken_ReturnsForbidden()
    {
        await using var factory = new CinemaWebApplicationFactory();
        using var client = CreateClient(factory, TestAuthTokens.Manager());

        var response = await client.PutAsJsonAsync(
            "/api/admin/banks/configured_bank",
            Request(isActive: true));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Upsert_DuplicateBin_ReturnsConflict()
    {
        await using var factory = new CinemaWebApplicationFactory();
        using var client = CreateClient(factory, TestAuthTokens.Admin());

        Assert.Equal(
            HttpStatusCode.Created,
            (await client.PutAsJsonAsync("/api/admin/banks/first_bank", Request(true))).StatusCode);

        var response = await client.PutAsJsonAsync(
            "/api/admin/banks/second_bank",
            Request(isActive: true));
        var body = await DeserializeAsync<ApiResponse<BankDirectoryResponse>>(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("BANK_BIN_DUPLICATE", body!.ErrorCode);
    }

    private static UpsertBankDirectoryRequest Request(bool isActive)
    {
        return new UpsertBankDirectoryRequest
        {
            BankBin = "CONFIGURED_BIN",
            ShortName = "Configured Bank",
            FullName = "Configured Bank From Database",
            IsActive = isActive
        };
    }

    private static HttpClient CreateClient(CinemaWebApplicationFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
        => JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync(), JsonOptions);
}
