using System.Net;
using System.Text;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Chatbot;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Movies;
using CinemaSystem.Contracts.Showtimes;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Infrastructure.Services;
using CinemaSystem.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace CinemaSystem.Tests;

public sealed class GeminiChatbotVoucherDisclosureTests
{
    [Fact]
    public async Task DependencyInjection_ResolvesRealGeminiChatbotService()
    {
        await using var factory = new CinemaWebApplicationFactory();
        using var scope = factory.Services.CreateScope();

        var service = scope.ServiceProvider.GetRequiredService<IChatbotService>();
        var voucherService = scope.ServiceProvider.GetRequiredService<IVoucherService>();

        Assert.IsType<GeminiChatbotService>(service);
        Assert.IsType<VoucherService>(voucherService);
    }

    [Fact]
    public async Task AskAsync_SendsOnlyPublicVoucherCodesToGemini()
    {
        var now = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);
        await using var db = CreateDbContext();
        db.Vouchers.AddRange(
            CreateVoucher(
                "VOU_PUBLIC",
                "PUBLIC_SAFE_20",
                now,
                isPrivate: false,
                DomainConstants.VoucherTargetType.AllCustomers),
            CreateVoucher(
                "VOU_PRIVATE",
                "PRIVATE_SECRET_100",
                now,
                isPrivate: true,
                DomainConstants.VoucherTargetType.SpecificCustomers,
                "CUS_OWNER",
                DomainConstants.VoucherCategory.Compensation),
            CreateVoucher(
                "VOU_ACCOUNT_BOUND",
                "ACCOUNT_ONLY_50",
                now,
                isPrivate: false,
                DomainConstants.VoucherTargetType.SpecificCustomers,
                "CUS_OWNER"),
            CreateVoucher(
                "VOU_EXPIRED_PUBLIC",
                "EXPIRED_PUBLIC_20",
                now.AddDays(-10),
                isPrivate: false,
                DomainConstants.VoucherTargetType.AllCustomers));
        await db.SaveChangesAsync();

        var clock = new FakeClock(now);
        var accessPolicy = new VoucherAccessPolicy(db, clock);
        var contextProvider = new ChatbotVoucherContextProvider(db, clock, accessPolicy);
        var handler = new RecordingGeminiHandler();
        var service = CreateService(
            db,
            clock,
            contextProvider,
            handler,
            exposePublicVouchers: true);

        var result = await service.AskAsync(
            new ChatbotRequest { Message = "Voucher hiện có?" },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("PUBLIC_SAFE_20", handler.RequestBody);
        Assert.DoesNotContain("PRIVATE_SECRET_100", handler.RequestBody);
        Assert.DoesNotContain("ACCOUNT_ONLY_50", handler.RequestBody);
        Assert.DoesNotContain("EXPIRED_PUBLIC_20", handler.RequestBody);
    }

    [Fact]
    public async Task AskAsync_WhenVoucherExposureDisabled_DoesNotLoadOrSendVoucherCodes()
    {
        var now = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);
        await using var db = CreateDbContext();
        var handler = new RecordingGeminiHandler();
        var provider = new Mock<IChatbotVoucherContextProvider>(MockBehavior.Strict);
        var service = CreateService(
            db,
            new FakeClock(now),
            provider.Object,
            handler,
            exposePublicVouchers: false);

        var result = await service.AskAsync(
            new ChatbotRequest { Message = "Cho tôi mã voucher" },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.DoesNotContain("Code:", handler.RequestBody);
        provider.VerifyNoOtherCalls();
    }

    private static Voucher CreateVoucher(
        string id,
        string code,
        DateTime now,
        bool isPrivate,
        string targetType,
        string? targetCustomerIds = null,
        string category = DomainConstants.VoucherCategory.Event)
    {
        return new Voucher
        {
            VoucherId = id,
            VoucherCode = code,
            Title = code,
            DiscountType = DomainConstants.DiscountType.Percent,
            DiscountValue = 20,
            UsageLimit = 10,
            UsedCount = 0,
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(5),
            VoucherStatus = DomainConstants.VoucherStatus.Active,
            Category = category,
            TargetType = targetType,
            TargetCustomerIds = targetCustomerIds,
            IsPrivate = isPrivate
        };
    }

    private static GeminiChatbotService CreateService(
        CinemaDbContext db,
        IClock clock,
        IChatbotVoucherContextProvider voucherContextProvider,
        RecordingGeminiHandler handler,
        bool exposePublicVouchers)
    {
        var movieService = new Mock<IMovieService>();
        movieService
            .Setup(service => service.GetMoviesAsync(
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<PagedList<MovieResponse>>.Ok(
                new PagedList<MovieResponse>([], 0, 1, 10)));

        var showtimeService = new Mock<IShowtimeService>();
        showtimeService
            .Setup(service => service.GetShowtimesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<IReadOnlyList<ShowtimeResponse>>.Ok([]));

        return new GeminiChatbotService(
            new HttpClient(handler),
            movieService.Object,
            showtimeService.Object,
            voucherContextProvider,
            Options.Create(new GeminiSettings
            {
                ApiKey = "test-api-key",
                ApiBaseUrl = "https://gemini.test/v1/models",
                Model = "test-model",
                ContextMovieLimit = 10
            }),
            Options.Create(new ChatbotSettings
            {
                ExposePublicVouchers = exposePublicVouchers
            }),
            db,
            clock);
    }

    private static CinemaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CinemaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new CinemaDbContext(options);
    }

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }

    private sealed class RecordingGeminiHandler : HttpMessageHandler
    {
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "candidates": [
                        {
                          "content": {
                            "parts": [
                              { "text": "Dạ, đây là thông tin khuyến mãi công khai." }
                            ]
                          }
                        }
                      ]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
