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
using CinemaSystem.Domain.Constants;
using Hangfire;

namespace CinemaSystem.Infrastructure.Services;

public class ReviewService : IReviewService
{
    // Khai báo DbContext để thao tác với cơ sở dữ liệu
    private readonly CinemaDbContext _dbContext;
    // Khai báo Service gọi AI để kiểm duyệt nội dung
    private readonly IAiModerationService _aiService;
    // Khai báo Service quản lý phim
    private readonly IMovieService _movieService;
    // Khai báo Hangfire để xử lý các tác vụ chạy nền (Background Jobs)
    private readonly Hangfire.IBackgroundJobClient _backgroundJobClient;
    // Khai báo cấu hình hệ thống
    private readonly CinemaSystem.Application.Settings.CinemaProcessingSettings _settings;

    // Phương thức khởi tạo (Constructor) nhận các dependency injection
    public ReviewService(CinemaDbContext dbContext, IAiModerationService aiService, IMovieService movieService, Hangfire.IBackgroundJobClient backgroundJobClient, Microsoft.Extensions.Options.IOptions<CinemaSystem.Application.Settings.CinemaProcessingSettings> options)
    {
        // Gán DbContext
        _dbContext = dbContext;
        // Gán AI Moderation Service
        _aiService = aiService;
        // Gán Movie Service
        _movieService = movieService;
        // Gán Background Job Client
        _backgroundJobClient = backgroundJobClient;
        // Lấy và gán giá trị các cấu hình Processing
        _settings = options.Value;
    }

    // Phương thức tạo bài đánh giá (Review) mới
    public async Task<ServiceResult<ReviewResponse>> CreateReviewAsync(string userId, CreateReviewRequest request, CancellationToken cancellationToken = default)
    {
        // Truy vấn thông tin tài khoản người dùng từ DB
        var userProfile = await _dbContext.Set<User>().FindAsync(new object[] { userId }, cancellationToken);
        // Kiểm tra xem tài khoản có đang bị khóa tính năng bình luận không
        if (userProfile != null && userProfile.IsBlocked && userProfile.BlockedUntil > DateTime.UtcNow)
        {
            // Nếu bị khóa, trả về lỗi 403 (Cấm truy cập)
            return ServiceResult<ReviewResponse>.Fail(403, "Tài khoản của bạn đang bị khóa tính năng bình luận.", "ACCOUNT_BLOCKED");
        }

        // Lấy thông tin Hồ sơ khách hàng tương ứng với UserId
        var customerProfile = await _dbContext.Set<CustomerProfile>()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
            
        // Nếu không tìm thấy hồ sơ khách hàng, trả về lỗi 400
        if (customerProfile == null)
            return ServiceResult<ReviewResponse>.Fail(400, "Customer profile not found for the user.", "CUSTOMER_PROFILE_NOT_FOUND");

        // Kiểm tra mã đặt vé truyền vào có hợp lệ không
        if (string.IsNullOrEmpty(request.BookingId))
            return ServiceResult<ReviewResponse>.Fail(400, "Bạn cần cung cấp mã đặt vé để đánh giá.", "BOOKING_REQUIRED");

        // Tìm kiếm thông tin vé đã đặt kèm theo suất chiếu của vé đó
        var booking = await _dbContext.Set<Booking>()
            // Bao gồm bảng Showtime
            .Include(b => b.Showtime)
            // Lọc theo mã BookingId
            .FirstOrDefaultAsync(b => b.BookingId == request.BookingId, cancellationToken);

        // Kiểm tra nếu không tìm thấy vé hoặc vé không thuộc về khách hàng này
        if (booking == null || booking.CustomerProfileId != customerProfile.CustomerProfileId)
            return ServiceResult<ReviewResponse>.Fail(400, "Không tìm thấy thông tin đặt vé hợp lệ.", "INVALID_BOOKING");

        // Chỉ cho phép đánh giá nếu vé đã thanh toán (Paid) hoặc đã hoàn tất (Completed)
        if (booking.BookingStatus != DomainConstants.EntityStatus.Completed && booking.BookingStatus != DomainConstants.EntityStatus.Paid)
            return ServiceResult<ReviewResponse>.Fail(400, "Chỉ được phép đánh giá sau khi hoàn tất thanh toán hoặc xem phim.", "BOOKING_NOT_COMPLETED");

        // Kiểm tra xem suất chiếu đã kết thúc chưa
        if (booking.Showtime.EndTime > DateTime.UtcNow)
            return ServiceResult<ReviewResponse>.Fail(400, "Bạn chỉ được phép đánh giá sau khi suất chiếu kết thúc.", "SHOWTIME_NOT_ENDED");

        // Kiểm tra xem khách hàng đã từng đánh giá cho vé này chưa
        bool alreadyReviewed = await _dbContext.Set<Review>().AnyAsync(r => r.BookingId == request.BookingId, cancellationToken);
        // Nếu đã đánh giá rồi thì báo lỗi để tránh spam
        if (alreadyReviewed)
            return ServiceResult<ReviewResponse>.Fail(400, "Bạn đã đánh giá cho vé này rồi.", "REVIEW_ALREADY_EXISTS");

        // Tạo một đối tượng Review mới
        var review = new Review
        {
            // Sinh ID ngẫu nhiên cho bài đánh giá
            ReviewId = Guid.NewGuid().ToString(),
            // Liên kết với Hồ sơ khách hàng
            CustomerProfileId = customerProfile.CustomerProfileId,
            // Liên kết với Phim được đánh giá
            MovieId = request.MovieId,
            // Liên kết với Mã vé
            BookingId = request.BookingId,
            // Số điểm đánh giá (Rating)
            Rating = request.Rating,
            // Nội dung bình luận
            Comment = request.Comment,
            // Thời điểm tạo đánh giá là thời gian hiện tại
            CreatedAt = DateTime.UtcNow,
            // Số lần chỉnh sửa khởi tạo bằng 0
            EditCount = 0
        };

        // Nếu nội dung bình luận rỗng (chỉ đánh giá sao)
        if (string.IsNullOrWhiteSpace(request.Comment))
        {
            // Cập nhật trạng thái là Đã duyệt (Approved) vì không có chữ nào để kiểm duyệt
            review.Status = ReviewConstants.Approved;
            // Thêm vào DbContext
            _dbContext.Set<Review>().Add(review);
            // Lưu dữ liệu vào Database
            await _dbContext.SaveChangesAsync(cancellationToken);
            // Trả về kết quả thành công ngay lập tức
            return ServiceResult<ReviewResponse>.Ok(MapToResponse(review));
        }

        // Nếu có nội dung bình luận, chuyển trạng thái thành "Đang chờ duyệt" (Pending)
        review.Status = ReviewConstants.Pending;
        // Thêm vào DbContext
        _dbContext.Set<Review>().Add(review);
        // Lưu dữ liệu sơ bộ vào Database để lấy ID
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Gọi hàm kiểm duyệt nội dung bằng AI (Bất đồng bộ)
        var aiMessage = await ProcessReviewModerationAsync(review.ReviewId, userId, request.Rating, request.Comment);

        // Trả về kết quả kèm theo thông điệp từ AI (nếu có) hoặc thông báo mặc định
        return ServiceResult<ReviewResponse>.Ok(MapToResponse(review), aiMessage ?? "Đánh giá của bạn đã được ghi nhận.", 200);
    }

    // Phương thức chỉnh sửa bài đánh giá đã có
    public async Task<ServiceResult<ReviewResponse>> EditReviewAsync(string userId, string reviewId, UpdateReviewRequest request, CancellationToken cancellationToken = default)
    {
        // Kiểm tra xem tài khoản có đang bị khóa hay không
        var userProfile = await _dbContext.Set<User>().FindAsync(new object[] { userId }, cancellationToken);
        // Nếu tài khoản bị khóa, từ chối cho phép sửa
        if (userProfile != null && userProfile.IsBlocked && userProfile.BlockedUntil > DateTime.UtcNow)
        {
            return ServiceResult<ReviewResponse>.Fail(403, "Tài khoản của bạn đang bị khóa tính năng bình luận.", "ACCOUNT_BLOCKED");
        }

        // Lấy thông tin Hồ sơ khách hàng
        var customerProfile = await _dbContext.Set<CustomerProfile>()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
            
        // Trả về lỗi nếu không tìm thấy hồ sơ
        if (customerProfile == null)
            return ServiceResult<ReviewResponse>.Fail(400, "Customer profile not found.", "CUSTOMER_PROFILE_NOT_FOUND");

        // Tìm bài đánh giá dựa vào ReviewId và xác minh nó thuộc về khách hàng này
        var review = await _dbContext.Set<Review>()
            .FirstOrDefaultAsync(r => r.ReviewId == reviewId && r.CustomerProfileId == customerProfile.CustomerProfileId, cancellationToken);

        // Báo lỗi nếu không tìm thấy bài đánh giá
        if (review == null)
            return ServiceResult<ReviewResponse>.Fail(404, "Review not found.", "REVIEW_NOT_FOUND");

        // Kiểm tra xem người dùng đã hết số lần sửa cho phép chưa (tối đa 1 lần)
        if (review.EditCount >= 1)
        {
            return ServiceResult<ReviewResponse>.Fail(400, "Bạn chỉ được phép chỉnh sửa bình luận 1 lần duy nhất!", "EDIT_LIMIT_EXCEEDED");
        }

        // Tăng bộ đếm số lần sửa lên 1
        review.EditCount += 1;
        // Cập nhật lại số điểm đánh giá
        review.Rating = request.Rating;
        // Cập nhật lại nội dung bình luận
        review.Comment = request.Comment;
        // Cập nhật lại thời gian sửa đổi bằng thời gian hiện tại
        review.CreatedAt = DateTime.UtcNow;

        // Nếu nội dung sửa đổi là rỗng
        if (string.IsNullOrWhiteSpace(request.Comment))
        {
            // Trạng thái được chuyển về Đã duyệt tự động
            review.Status = ReviewConstants.Approved;
            // Xóa lý do từ chối cũ (nếu có)
            review.RejectedReason = null;
            // Xóa thông tin người kiểm duyệt
            review.ModeratedBy = null;
            // Lưu dữ liệu vào DB
            await _dbContext.SaveChangesAsync(cancellationToken);
            // Trả về thành công
            return ServiceResult<ReviewResponse>.Ok(MapToResponse(review));
        }

        // Nếu có text, chuyển trạng thái về Pending để kiểm duyệt lại
        review.Status = ReviewConstants.Pending;
        // Lưu tạm thời trạng thái Pending
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Gửi nội dung mới qua cho AI kiểm duyệt
        var aiMessage = await ProcessReviewModerationAsync(review.ReviewId, userId, request.Rating, request.Comment);

        // Trả về kết quả
        return ServiceResult<ReviewResponse>.Ok(MapToResponse(review), aiMessage ?? "Đánh giá của bạn đã được ghi nhận.", 200);
    }

    // Xử lý kiểm duyệt nội dung đánh giá bằng AI
    public async Task<string?> ProcessReviewModerationAsync(string reviewId, string userId, int rating, string comment)
    {
        // Lấy lại bài đánh giá từ DB
        var review = await _dbContext.Set<Review>().FindAsync(new object[] { reviewId });
        // Nếu không tìm thấy, thoát hàm
        if (review == null) return null;

        // Gọi hàm xử lý logic kiểm duyệt chi tiết
        var moderationResult = await HandleModerationAsync(userId, rating, comment, CancellationToken.None);
        
        // Nếu kết quả trả về là tài khoản bị khóa hoặc nội dung vi phạm nặng
        if (moderationResult.IsBlockedOrFailed)
        {
            // Chuyển trạng thái Review sang Từ chối (Rejected)
            review.Status = ReviewConstants.Rejected;
            // Lưu lại lý do từ chối, nếu AI không cung cấp thì dùng mặc định
            review.RejectedReason = moderationResult.AiResult?.Reason ?? "Tài khoản vi phạm.";
            // Gỡ người kiểm duyệt vì đây là AI làm
            review.ModeratedBy = null;
        }
        else
        {
            // Gán trạng thái dựa vào phản hồi của AI
            review.Status = GetStatusConstant(moderationResult.AiResult.Status);
            // Nếu AI xác định là Rejected hoặc Flagged (Cắm cờ nghi ngờ)
            if (review.Status == ReviewConstants.Rejected || review.Status == ReviewConstants.Flagged)
            {
                // Lưu lại lý do
                review.RejectedReason = moderationResult.AiResult.Reason;
                // Gỡ người duyệt
                review.ModeratedBy = null;
            }
            else
            {
                // Nếu Approved thì xóa lý do từ chối
                review.RejectedReason = null;
                // Gỡ người duyệt
                review.ModeratedBy = null;
            }
        }
        
        // Lưu thay đổi vào DB
        await _dbContext.SaveChangesAsync();

        // Trả về thông điệp từ Moderator (AI) để hiển thị cho User
        return moderationResult.AiResult?.ModeratorMessage;
    }

    // Lớp chứa kết quả trả về từ hàm kiểm duyệt
    private class ModerationHandlerResult
    {
        // Cờ xác định xem user có bị block hoặc thao tác thất bại không
        public bool IsBlockedOrFailed { get; set; }
        // Mã HTTP status code để trả về
        public int StatusCode { get; set; }
        // Câu thông báo
        public string Message { get; set; } = string.Empty;
        // Đối tượng chi tiết kết quả AI trả về
        public AiModerationResult AiResult { get; set; } = null!;
    }

    // Hàm gọi AI và xử lý các luật khóa tài khoản (Spam)
    private async Task<ModerationHandlerResult> HandleModerationAsync(string userId, int rating, string comment, CancellationToken cancellationToken)
    {
        // Gọi service AI để phân tích nội dung
        var aiResponse = await _aiService.ModerateReviewAsync(rating, comment, cancellationToken);

        // Nếu AI trả về Approved
        if (string.Equals(aiResponse.Status, ReviewConstants.Approved, StringComparison.OrdinalIgnoreCase))
        {
            // Trả về kết quả an toàn
            return new ModerationHandlerResult { IsBlockedOrFailed = false, AiResult = aiResponse };
        }
        // Nếu AI trả về Rejected hoặc Flagged
        else if (string.Equals(aiResponse.Status, ReviewConstants.Rejected, StringComparison.OrdinalIgnoreCase) || string.Equals(aiResponse.Status, ReviewConstants.Flagged, StringComparison.OrdinalIgnoreCase))
        {
            // Kiểm tra xem AI có coi đây là Spam hoặc vi phạm nghiêm trọng (Rejected) không
            if (aiResponse.IsSpam || string.Equals(aiResponse.Status, ReviewConstants.Rejected, StringComparison.OrdinalIgnoreCase))
            {
                // Lấy thông tin user
                var userProfile = await _dbContext.Set<User>().FindAsync(new object[] { userId }, cancellationToken);
                if (userProfile != null)
                {
                    // Nếu user chưa vi phạm Spam lần nào
                    if (userProfile.SpamViolationCount == 0)
                    {
                        // Tăng biến đếm số lần vi phạm Spam lên 1
                        userProfile.SpamViolationCount += 1;
                        // Trả về cảnh báo khóa tài khoản sớm (Cảnh cáo)
                        return new ModerationHandlerResult 
                        { 
                            IsBlockedOrFailed = true, 
                            StatusCode = 400, 
                            Message = $"{aiResponse.ModeratorMessage} Đây là lời cảnh báo. Nếu vi phạm lần nữa, tài khoản của bạn sẽ bị khóa {_settings.ReviewSpamLockoutWarningDays} ngày!",
                            AiResult = aiResponse
                        };
                    }
                    else // Nếu user đã từng vi phạm Spam rồi
                    {
                        // Kích hoạt trạng thái Block
                        userProfile.IsBlocked = true;
                        // Đặt thời gian mở khóa dựa theo tham số hệ thống
                        userProfile.BlockedUntil = DateTime.UtcNow.AddMinutes(_settings.ReviewSpamLockoutMinutes);
                        // Trả về kết quả Block tài khoản
                        return new ModerationHandlerResult 
                        { 
                            IsBlockedOrFailed = true, 
                            StatusCode = 403, 
                            Message = $"Tài khoản của bạn đã bị khóa {_settings.ReviewSpamLockoutMinutes} phút do vi phạm quy định bình luận nhiều lần.",
                            AiResult = aiResponse
                        };
                    }
                }
            }
            
            // Nếu chỉ bị đánh dấu Cắm cờ (Flagged) nhưng không phải là Spam, cứ trả về trạng thái bình thường (Cần admin duyệt tay)
            return new ModerationHandlerResult { IsBlockedOrFailed = false, AiResult = aiResponse };
        }

        // Trả về mặc định
        return new ModerationHandlerResult { IsBlockedOrFailed = false, AiResult = aiResponse };
    }

    // Hàm ánh xạ chuỗi trạng thái thành Hằng số (Constants) an toàn
    private string GetStatusConstant(string aiStatus)
    {
        // Map về Rejected
        if (string.Equals(aiStatus, ReviewConstants.Rejected, StringComparison.OrdinalIgnoreCase)) return ReviewConstants.Rejected;
        // Map về Flagged
        if (string.Equals(aiStatus, ReviewConstants.Flagged, StringComparison.OrdinalIgnoreCase)) return ReviewConstants.Flagged;
        // Mặc định là Approved
        return ReviewConstants.Approved;
    }

    // Hàm lấy danh sách các đánh giá đã được duyệt (Approved) của một bộ phim
    public async Task<ServiceResult<List<ReviewResponse>>> GetApprovedMovieReviewsAsync(string movieId, CancellationToken cancellationToken = default)
    {
        // Truy vấn bảng Review
        var reviews = await _dbContext.Set<Review>()
            // Bỏ qua tracking cho hiệu năng vì chỉ đọc dữ liệu
            .AsNoTracking()
            // Lọc theo mã phim và trạng thái Approved
            .Where(r => r.MovieId == movieId && r.Status == ReviewConstants.Approved)
            // Sắp xếp mới nhất lên đầu
            .OrderByDescending(r => r.CreatedAt)
            // Chuyển kết quả ra List
            .ToListAsync(cancellationToken);

        // Ánh xạ sang Response DTO và trả về
        return ServiceResult<List<ReviewResponse>>.Ok(reviews.Select(MapToResponse).ToList());
    }

    // Hàm cập nhật trạng thái của một bình luận (Dành cho Admin)
    public async Task<ServiceResult<bool>> UpdateReviewStatusAsync(string reviewId, string status, CancellationToken cancellationToken = default)
    {
        // Tìm kiếm Review
        var review = await _dbContext.Set<Review>().FindAsync(new object[] { reviewId }, cancellationToken);
        // Báo lỗi nếu không có
        if (review == null) return ServiceResult<bool>.Fail(404, "Review not found.", "REVIEW_NOT_FOUND");

        // Thay đổi trạng thái
        review.Status = status;
        // Lưu xuống DB
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Báo thành công
        return ServiceResult<bool>.Ok(true);
    }

    // Admin duyệt tay một bình luận bị đánh dấu
    public async Task<ServiceResult<bool>> AdminApproveReviewAsync(string reviewId, string adminUserId, CancellationToken cancellationToken = default)
    {
        // Tìm Review
        var review = await _dbContext.Set<Review>().FindAsync(new object[] { reviewId }, cancellationToken);
        // Nếu không có
        if (review == null) return ServiceResult<bool>.Fail(404, "Không tìm thấy đánh giá.", "REVIEW_NOT_FOUND");

        // Chuyển sang Approved
        review.Status = ReviewConstants.Approved;
        // Ghi nhận Admin nào đã duyệt
        review.ModeratedBy = adminUserId;
        // Xóa lý do từ chối
        review.RejectedReason = null;

        // Lấy Profile người đã viết bình luận
        var customerProfile = await _dbContext.Set<CustomerProfile>()
            .FirstOrDefaultAsync(c => c.CustomerProfileId == review.CustomerProfileId, cancellationToken);

        // Nếu người này tồn tại
        if (customerProfile != null)
        {
            // Lấy ra User tương ứng
            var userProfile = await _dbContext.Set<User>().FindAsync(new object[] { customerProfile.UserId }, cancellationToken);
            if (userProfile != null)
            {
                // Nếu họ từng bị cảnh cáo Spam thì giảm nhẹ tội (trừ đi 1)
                if (userProfile.SpamViolationCount > 0)
                {
                    userProfile.SpamViolationCount -= 1;
                }

                // Nếu tài khoản họ đang bị khóa, mở khóa lại luôn do Admin đã can thiệp gỡ oan
                if (userProfile.IsBlocked && userProfile.BlockedUntil > DateTime.UtcNow)
                {
                    userProfile.IsBlocked = false;
                    userProfile.BlockedUntil = null;
                }
            }
        }

        // Lưu vào DB
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Báo thành công
        return ServiceResult<bool>.Ok(true);
    }

    // Hàm chuyển đổi Model sang Response
    private ReviewResponse MapToResponse(Review review)
    {
        // Map từng field tương ứng
        return new ReviewResponse
        {
            // ID đánh giá
            ReviewId = review.ReviewId,
            // ID Profile khách hàng
            CustomerProfileId = review.CustomerProfileId,
            // ID Phim
            MovieId = review.MovieId,
            // ID Đặt vé
            BookingId = review.BookingId,
            // Điểm rating
            Rating = review.Rating,
            // Lời bình
            Comment = review.Comment,
            // Ngày tạo
            CreatedAt = review.CreatedAt,
            // Trạng thái hiện tại
            Status = review.Status
        };
    }

    // Lấy danh sách lịch sử đánh giá của chính khách hàng đó
    public async Task<ServiceResult<List<ReviewResponse>>> GetCustomerReviewsAsync(string userId, CancellationToken cancellationToken = default)
    {
        // Lấy Customer Profile
        var customerProfile = await _dbContext.Set<CustomerProfile>()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        // Lỗi nếu không tìm thấy
        if (customerProfile == null)
            return ServiceResult<List<ReviewResponse>>.Fail(400, "Customer profile not found.", "CUSTOMER_PROFILE_NOT_FOUND");

        // Tìm tất cả Review thuộc về Profile này
        var reviews = await _dbContext.Set<Review>()
            // NoTracking cho nhanh
            .AsNoTracking()
            // Điều kiện lọc theo CustomerProfileId
            .Where(r => r.CustomerProfileId == customerProfile.CustomerProfileId)
            // Sắp xếp mới nhất lên đầu
            .OrderByDescending(r => r.CreatedAt)
            // Đưa ra list
            .ToListAsync(cancellationToken);

        // Ánh xạ ra Response và trả về
        return ServiceResult<List<ReviewResponse>>.Ok(reviews.Select(MapToResponse).ToList());
    }
}
