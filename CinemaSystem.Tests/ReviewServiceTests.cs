using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Reviews;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CinemaSystem.Tests;

public sealed class ReviewServiceTests
{
    private static CinemaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CinemaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new CinemaDbContext(options);
    }

    private static ReviewService CreateService(
        CinemaDbContext dbContext,
        IAiModerationService? aiModerationService = null)
    {
        return new ReviewService(
            dbContext,
            aiModerationService ?? new Mock<IAiModerationService>().Object,
            new Mock<IMovieService>().Object,
            new Mock<Hangfire.IBackgroundJobClient>().Object,
            Microsoft.Extensions.Options.Options.Create(
                new CinemaSystem.Application.Settings.CinemaProcessingSettings()));
    }

    private static void SeedCompletedBooking(CinemaDbContext dbContext)
    {
        dbContext.Set<Showtime>().Add(new Showtime
        {
            ShowtimeId = "SHW1",
            MovieId = "MOV1",
            RoomId = "ROOM1",
            StartTime = DateTime.UtcNow.AddHours(-3),
            EndTime = DateTime.UtcNow.AddHours(-1),
            BasePrice = 90000m,
            Status = "COMPLETED",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });
        dbContext.Set<Booking>().Add(new Booking
        {
            BookingId = "BKG1",
            ShowtimeId = "SHW1",
            CustomerProfileId = "CP1",
            BookingStatus = "PAID",
            TotalAmount = 90000m,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            BookingChannel = "ONLINE"
        });
    }

    [Fact]
    public async Task CreateReviewAsync_WhenCustomerProfileNotFound_ReturnsFail()
    {
        var dbContext = CreateDbContext();
        var aiModerationServiceMock = new Mock<IAiModerationService>();
        var service = CreateService(dbContext, aiModerationServiceMock.Object);

        var request = new CreateReviewRequest { MovieId = "MOV1", Rating = 5 };

        var result = await service.CreateReviewAsync("user123", request);

        Assert.False(result.Success);
        Assert.Equal("CUSTOMER_PROFILE_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task CreateReviewAsync_WithNoComment_IsApproved()
    {
        var dbContext = CreateDbContext();
        dbContext.Set<CustomerProfile>().Add(new CustomerProfile { CustomerProfileId = "CP1", UserId = "user123", MemberLevel = "STANDARD" });
        SeedCompletedBooking(dbContext);
        await dbContext.SaveChangesAsync();

        var aiModerationServiceMock = new Mock<IAiModerationService>();
        var service = CreateService(dbContext, aiModerationServiceMock.Object);

        var request = new CreateReviewRequest { MovieId = "MOV1", BookingId = "BKG1", Rating = 5, Comment = "" };

        var result = await service.CreateReviewAsync("user123", request);

        Assert.True(result.Success);
        Assert.Equal(ReviewConstants.Approved, result.Data!.Status);

        var savedReview = await dbContext.Set<Review>().FirstOrDefaultAsync(r => r.ReviewId == result.Data.ReviewId);
        Assert.NotNull(savedReview);
        Assert.Equal(ReviewConstants.Approved, savedReview.Status);
    }

    [Fact]
    public async Task CreateReviewAsync_WithComment_ApprovedByAi()
    {
        var dbContext = CreateDbContext();
        dbContext.Set<CustomerProfile>().Add(new CustomerProfile { CustomerProfileId = "CP1", UserId = "user123", MemberLevel = "STANDARD" });
        SeedCompletedBooking(dbContext);
        await dbContext.SaveChangesAsync();

        var aiModerationServiceMock = new Mock<IAiModerationService>();
        aiModerationServiceMock.Setup(x => x.ModerateReviewAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiModerationResult { Status = "APPROVED" });

        var service = CreateService(dbContext, aiModerationServiceMock.Object);

        var request = new CreateReviewRequest { MovieId = "MOV1", BookingId = "BKG1", Rating = 5, Comment = "Great movie!" };

        var result = await service.CreateReviewAsync("user123", request);

        Assert.True(result.Success);
        Assert.Equal("APPROVED", result.Data!.Status);

        await service.ProcessReviewModerationAsync(
            result.Data.ReviewId,
            "user123",
            request.Rating,
            request.Comment!);

        var savedReview = await dbContext.Set<Review>().FirstOrDefaultAsync(r => r.ReviewId == result.Data.ReviewId);
        Assert.NotNull(savedReview);
        Assert.Equal(ReviewConstants.Approved, savedReview.Status);
    }

    [Fact]
    public async Task GetApprovedMovieReviewsAsync_ReturnsOnlyApprovedReviews()
    {
        var dbContext = CreateDbContext();
        dbContext.Set<Review>().AddRange(
            new Review { ReviewId = "R1", CustomerProfileId = "C1", MovieId = "M1", Rating = 5, Status = ReviewConstants.Approved, CreatedAt = DateTime.UtcNow },
            new Review { ReviewId = "R2", CustomerProfileId = "C2", MovieId = "M1", Rating = 4, Status = ReviewConstants.Pending, CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
            new Review { ReviewId = "R3", CustomerProfileId = "C3", MovieId = "M1", Rating = 3, Status = ReviewConstants.Flagged, CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
            new Review { ReviewId = "R4", CustomerProfileId = "C4", MovieId = "M2", Rating = 5, Status = ReviewConstants.Approved, CreatedAt = DateTime.UtcNow }
        );
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.GetApprovedMovieReviewsAsync("M1");

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal("R1", result.Data[0].ReviewId);
    }

    [Fact]
    public async Task UpdateReviewStatusAsync_WhenReviewExists_UpdatesStatus()
    {
        var dbContext = CreateDbContext();
        var review = new Review { ReviewId = "R1", CustomerProfileId = "C1", MovieId = "M1", Rating = 5, Status = ReviewConstants.Pending };
        dbContext.Set<Review>().Add(review);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.UpdateReviewStatusAsync("R1", ReviewConstants.Approved);

        Assert.True(result.Success);
        Assert.True(result.Data);

        var updatedReview = await dbContext.Set<Review>().FirstOrDefaultAsync(r => r.ReviewId == "R1");
        Assert.Equal(ReviewConstants.Approved, updatedReview!.Status);
    }
}
