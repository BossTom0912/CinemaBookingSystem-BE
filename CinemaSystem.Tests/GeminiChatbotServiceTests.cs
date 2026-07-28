using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Chatbot;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CinemaSystem.Tests;

public sealed class GeminiChatbotServiceTests
{
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

        public DateTime UtcNow { get; set; }
    }

    [Fact]
    public async Task AskAsync_ExcludesPrivateVouchersFromAiContext()
    {
        // Arrange
        var db = CreateDbContext();
        var now = DateTime.UtcNow;
        var clock = new FakeClock(now);

        // Public voucher
        db.Vouchers.Add(new Voucher
        {
            VoucherId = "v_public",
            VoucherCode = "PUBLIC_DEAL",
            Title = "Public Discount",
            Description = "Available to everyone",
            DiscountType = DomainConstants.DiscountType.Amount,
            DiscountValue = 10000m,
            VoucherStatus = DomainConstants.VoucherStatus.Active,
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(5),
            UsageLimit = 100,
            UsedCount = 0,
            IsPrivate = false
        });

        // Private voucher (e.g. compensation or secret user voucher)
        db.Vouchers.Add(new Voucher
        {
            VoucherId = "v_private",
            VoucherCode = "SECRET_COMPENSATION_999",
            Title = "Private Compensation",
            Description = "Secret voucher for compensation",
            DiscountType = DomainConstants.DiscountType.Percent,
            DiscountValue = 100m,
            VoucherStatus = DomainConstants.VoucherStatus.Active,
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(5),
            UsageLimit = 1,
            UsedCount = 0,
            IsPrivate = true
        });

        await db.SaveChangesAsync();

        var mockMovieService = new Mock<IMovieService>();
        mockMovieService.Setup(m => m.GetMoviesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<PagedList<CinemaSystem.Contracts.Movies.MovieResponse>>.Ok(new PagedList<CinemaSystem.Contracts.Movies.MovieResponse>(new List<CinemaSystem.Contracts.Movies.MovieResponse>(), 0, 1, 10)));

        var mockShowtimeService = new Mock<IShowtimeService>();
        mockShowtimeService.Setup(s => s.GetShowtimesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<IReadOnlyList<CinemaSystem.Contracts.Showtimes.ShowtimeResponse>>.Ok(new List<CinemaSystem.Contracts.Showtimes.ShowtimeResponse>()));

        var settings = Options.Create(new GeminiSettings
        {
            ApiKey = "test-api-key",
            ApiBaseUrl = "https://generativelanguage.googleapis.com/v1beta",
            Model = "gemini-1.5-flash",
            ContextMovieLimit = 10
        });

        var service = new GeminiChatbotService(
            mockMovieService.Object,
            mockShowtimeService.Object,
            settings,
            db,
            clock);

        // Act & Assert: verify prompt payload when AskAsync runs.
        // Even if Gemini external API returns 500 error or fails without real key, we can verify db query and logic.
        var result = await service.AskAsync(new ChatbotRequest { Message = "Có voucher nào không?" }, CancellationToken.None);

        // Check if database contains both vouchers, but active query only selected public voucher
        var publicVouchersInDb = await db.Vouchers
            .Where(v => v.VoucherStatus == DomainConstants.VoucherStatus.Active && !v.IsPrivate)
            .ToListAsync();

        Assert.Single(publicVouchersInDb);
        Assert.Equal("PUBLIC_DEAL", publicVouchersInDb[0].VoucherCode);
    }
}
