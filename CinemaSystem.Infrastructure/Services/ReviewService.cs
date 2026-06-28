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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Hangfire;

namespace CinemaSystem.Infrastructure.Services;

public class ReviewService : IReviewService
{
    private readonly CinemaDbContext _dbContext;
    private readonly IAiModerationService _aiService;
    private readonly IMovieService _movieService;
    private readonly Hangfire.IBackgroundJobClient _backgroundJobClient;

    public ReviewService(CinemaDbContext dbContext, IAiModerationService aiService, IMovieService movieService, Hangfire.IBackgroundJobClient backgroundJobClient)
    {
        _dbContext = dbContext;
        _aiService = aiService;
        _movieService = movieService;
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task<ServiceResult<ReviewResponse>> CreateReviewAsync(string userId, CreateReviewRequest request, CancellationToken cancellationToken = default)
    {
        var userProfile = await _dbContext.Set<User>().FindAsync(new object[] { userId }, cancellationToken);
        if (userProfile != null && userProfile.IsBlocked && userProfile.BlockedUntil > DateTime.UtcNow)
        {
            return ServiceResult<ReviewResponse>.Fail(403, "Tài khoản của bạn đang bị khóa tính năng bình luận.", "ACCOUNT_BLOCKED");
        }

        var customerProfile = await _dbContext.Set<CustomerProfile>()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (customerProfile == null)
            return ServiceResult<ReviewResponse>.Fail(400, "Customer profile not found for the user.", "CUSTOMER_PROFILE_NOT_FOUND");

        if (string.IsNullOrEmpty(request.BookingId))
            return ServiceResult<ReviewResponse>.Fail(400, "Bạn cần cung cấp mã đặt vé để đánh giá.", "BOOKING_REQUIRED");

        var booking = await _dbContext.Set<Booking>()
            .Include(b => b.Showtime)
            .FirstOrDefaultAsync(b => b.BookingId == request.BookingId, cancellationToken);

        if (booking == null || booking.CustomerProfileId != customerProfile.CustomerProfileId)
            return ServiceResult<ReviewResponse>.Fail(400, "Không tìm thấy thông tin đặt vé hợp lệ.", "INVALID_BOOKING");

        if (booking.BookingStatus != "COMPLETED" && booking.BookingStatus != "PAID")
            return ServiceResult<ReviewResponse>.Fail(400, "Chỉ được phép đánh giá sau khi hoàn tất thanh toán hoặc xem phim.", "BOOKING_NOT_COMPLETED");

        if (booking.Showtime.EndTime > DateTime.UtcNow)
            return ServiceResult<ReviewResponse>.Fail(400, "Bạn chỉ được phép đánh giá sau khi suất chiếu kết thúc.", "SHOWTIME_NOT_ENDED");

        bool alreadyReviewed = await _dbContext.Set<Review>().AnyAsync(r => r.BookingId == request.BookingId, cancellationToken);
        if (alreadyReviewed)
            return ServiceResult<ReviewResponse>.Fail(400, "Bạn đã đánh giá cho vé này rồi.", "REVIEW_ALREADY_EXISTS");

        var review = new Review
        {
            ReviewId = Guid.NewGuid().ToString(),
            CustomerProfileId = customerProfile.CustomerProfileId,
            MovieId = request.MovieId,
            BookingId = request.BookingId,
            Rating = request.Rating,
            Comment = request.Comment,
            CreatedAt = DateTime.UtcNow,
            EditCount = 0
        };

        if (string.IsNullOrWhiteSpace(request.Comment))
        {
            review.Status = ReviewConstants.Approved;
            _dbContext.Set<Review>().Add(review);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<ReviewResponse>.Ok(MapToResponse(review));
        }

        review.Status = "PENDING";
        _dbContext.Set<Review>().Add(review);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _backgroundJobClient.Enqueue<IReviewService>(s => s.ProcessReviewModerationAsync(review.ReviewId, userId, request.Rating, request.Comment));

        return ServiceResult<ReviewResponse>.Ok(MapToResponse(review), "Bình luận của bạn đang được duyệt.", 202);
    }

    public async Task<ServiceResult<ReviewResponse>> EditReviewAsync(string userId, string reviewId, UpdateReviewRequest request, CancellationToken cancellationToken = default)
    {
        var userProfile = await _dbContext.Set<User>().FindAsync(new object[] { userId }, cancellationToken);
        if (userProfile != null && userProfile.IsBlocked && userProfile.BlockedUntil > DateTime.UtcNow)
        {
            return ServiceResult<ReviewResponse>.Fail(403, "Tài khoản của bạn đang bị khóa tính năng bình luận.", "ACCOUNT_BLOCKED");
        }

        var customerProfile = await _dbContext.Set<CustomerProfile>()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (customerProfile == null)
            return ServiceResult<ReviewResponse>.Fail(400, "Customer profile not found.", "CUSTOMER_PROFILE_NOT_FOUND");

        var review = await _dbContext.Set<Review>()
            .FirstOrDefaultAsync(r => r.ReviewId == reviewId && r.CustomerProfileId == customerProfile.CustomerProfileId, cancellationToken);

        if (review == null)
            return ServiceResult<ReviewResponse>.Fail(404, "Review not found.", "REVIEW_NOT_FOUND");

        if (review.EditCount >= 1)
        {
            return ServiceResult<ReviewResponse>.Fail(400, "Bạn chỉ được phép chỉnh sửa bình luận 1 lần duy nhất!", "EDIT_LIMIT_EXCEEDED");
        }

        review.EditCount += 1;
        review.Rating = request.Rating;
        review.Comment = request.Comment;
        review.CreatedAt = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(request.Comment))
        {
            review.Status = ReviewConstants.Approved;
            review.RejectedReason = null;
            review.ModeratedBy = null;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<ReviewResponse>.Ok(MapToResponse(review));
        }

        review.Status = "PENDING";
        await _dbContext.SaveChangesAsync(cancellationToken);

        _backgroundJobClient.Enqueue<IReviewService>(s => s.ProcessReviewModerationAsync(review.ReviewId, userId, request.Rating, request.Comment));

        return ServiceResult<ReviewResponse>.Ok(MapToResponse(review), "Bình luận của bạn đang được duyệt.", 202);
    }

    public async Task ProcessReviewModerationAsync(string reviewId, string userId, int rating, string comment)
    {
        var review = await _dbContext.Set<Review>().FindAsync(new object[] { reviewId });
        if (review == null) return;

        var moderationResult = await HandleModerationAsync(userId, rating, comment, CancellationToken.None);

        if (moderationResult.IsBlockedOrFailed)
        {
            review.Status = ReviewConstants.Rejected;
            review.RejectedReason = moderationResult.AiResult?.Reason ?? "Tài khoản vi phạm.";
            // moderatedBy is a USER foreign key; null identifies automated moderation.
            review.ModeratedBy = null;
        }
        else
        {
            review.Status = GetStatusConstant(moderationResult.AiResult.Status);
            if (review.Status == ReviewConstants.Rejected || review.Status == ReviewConstants.Flagged)
            {
                review.RejectedReason = moderationResult.AiResult.Reason;
                review.ModeratedBy = null;
            }
            else
            {
                review.RejectedReason = null;
                review.ModeratedBy = null;
            }
        }

        await _dbContext.SaveChangesAsync();
    }

    private class ModerationHandlerResult
    {
        public bool IsBlockedOrFailed { get; set; }
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public AiModerationResult AiResult { get; set; } = null!;
    }

    private async Task<ModerationHandlerResult> HandleModerationAsync(string userId, int rating, string comment, CancellationToken cancellationToken)
    {
        var aiResponse = await _aiService.ModerateReviewAsync(rating, comment, cancellationToken);

        if (aiResponse.Status == "APPROVED")
        {
            return new ModerationHandlerResult { IsBlockedOrFailed = false, AiResult = aiResponse };
        }
        else if (aiResponse.Status == "REJECTED" || aiResponse.Status == "FLAGGED")
        {
            if (aiResponse.IsSpam || aiResponse.Status == "REJECTED")
            {
                var userProfile = await _dbContext.Set<User>().FindAsync(new object[] { userId }, cancellationToken);
                if (userProfile != null)
                {
                    if (userProfile.SpamViolationCount == 0)
                    {
                        userProfile.SpamViolationCount += 1;
                        // Return early with block/warning status
                        return new ModerationHandlerResult
                        {
                            IsBlockedOrFailed = true,
                            StatusCode = 400,
                            Message = $"{aiResponse.ModeratorMessage} Đây là lời cảnh báo. Nếu vi phạm lần nữa, tài khoản của bạn sẽ bị khóa 1 tuần!",
                            AiResult = aiResponse
                        };
                    }
                    else
                    {
                        userProfile.IsBlocked = true;
                        userProfile.BlockedUntil = DateTime.UtcNow.AddMinutes(1);
                        return new ModerationHandlerResult
                        {
                            IsBlockedOrFailed = true,
                            StatusCode = 403,
                            Message = "Tài khoản của bạn đã bị khóa 1 phút do vi phạm quy định bình luận nhiều lần.",
                            AiResult = aiResponse
                        };
                    }
                }
            }

            // If flagged but not spam, just save it as flagged
            return new ModerationHandlerResult { IsBlockedOrFailed = false, AiResult = aiResponse };
        }

        return new ModerationHandlerResult { IsBlockedOrFailed = false, AiResult = aiResponse };
    }

    private string GetStatusConstant(string aiStatus)
    {
        if (aiStatus == "REJECTED") return ReviewConstants.Rejected;
        if (aiStatus == "FLAGGED") return ReviewConstants.Flagged;
        return ReviewConstants.Approved;
    }

    public async Task<ServiceResult<List<ReviewResponse>>> GetApprovedMovieReviewsAsync(string movieId, CancellationToken cancellationToken = default)
    {
        var reviews = await _dbContext.Set<Review>()
            .AsNoTracking()
            .Where(r => r.MovieId == movieId && r.Status == ReviewConstants.Approved)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return ServiceResult<List<ReviewResponse>>.Ok(reviews.Select(MapToResponse).ToList());
    }

    public async Task<ServiceResult<bool>> UpdateReviewStatusAsync(string reviewId, string status, CancellationToken cancellationToken = default)
    {
        var review = await _dbContext.Set<Review>().FindAsync(new object[] { reviewId }, cancellationToken);
        if (review == null) return ServiceResult<bool>.Fail(404, "Review not found.", "REVIEW_NOT_FOUND");

        review.Status = status;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<bool>> AdminApproveReviewAsync(string reviewId, string adminUserId, CancellationToken cancellationToken = default)
    {
        var review = await _dbContext.Set<Review>().FindAsync(new object[] { reviewId }, cancellationToken);
        if (review == null) return ServiceResult<bool>.Fail(404, "Không tìm thấy đánh giá.", "REVIEW_NOT_FOUND");

        review.Status = ReviewConstants.Approved;
        review.ModeratedBy = adminUserId;
        review.RejectedReason = null;

        var customerProfile = await _dbContext.Set<CustomerProfile>()
            .FirstOrDefaultAsync(c => c.CustomerProfileId == review.CustomerProfileId, cancellationToken);

        if (customerProfile != null)
        {
            var userProfile = await _dbContext.Set<User>().FindAsync(new object[] { customerProfile.UserId }, cancellationToken);
            if (userProfile != null)
            {
                if (userProfile.SpamViolationCount > 0)
                {
                    userProfile.SpamViolationCount -= 1;
                }

                if (userProfile.IsBlocked && userProfile.BlockedUntil > DateTime.UtcNow)
                {
                    userProfile.IsBlocked = false;
                    userProfile.BlockedUntil = null;
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true);
    }

    private ReviewResponse MapToResponse(Review review)
    {
        return new ReviewResponse
        {
            ReviewId = review.ReviewId,
            CustomerProfileId = review.CustomerProfileId,
            MovieId = review.MovieId,
            BookingId = review.BookingId,
            Rating = review.Rating,
            Comment = review.Comment,
            CreatedAt = review.CreatedAt,
            Status = review.Status
        };
    }

    public async Task<ServiceResult<List<ReviewResponse>>> GetCustomerReviewsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var customerProfile = await _dbContext.Set<CustomerProfile>()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (customerProfile == null)
            return ServiceResult<List<ReviewResponse>>.Fail(400, "Customer profile not found.", "CUSTOMER_PROFILE_NOT_FOUND");

        var reviews = await _dbContext.Set<Review>()
            .AsNoTracking()
            .Where(r => r.CustomerProfileId == customerProfile.CustomerProfileId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return ServiceResult<List<ReviewResponse>>.Ok(reviews.Select(MapToResponse).ToList());
    }
}
