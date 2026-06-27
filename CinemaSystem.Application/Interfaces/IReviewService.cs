using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Reviews;

namespace CinemaSystem.Application.Interfaces;

public interface IReviewService
{
    Task<ServiceResult<ReviewResponse>> CreateReviewAsync(string customerProfileId, CreateReviewRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<ReviewResponse>> EditReviewAsync(string userId, string reviewId, UpdateReviewRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<List<ReviewResponse>>> GetApprovedMovieReviewsAsync(string movieId, CancellationToken cancellationToken = default);
    Task<ServiceResult<bool>> UpdateReviewStatusAsync(string reviewId, string status, CancellationToken cancellationToken = default);
    Task<ServiceResult<bool>> AdminApproveReviewAsync(string reviewId, string adminUserId, CancellationToken cancellationToken = default);
    Task<ServiceResult<List<ReviewResponse>>> GetCustomerReviewsAsync(string customerProfileId, CancellationToken cancellationToken = default);
    Task ProcessReviewModerationAsync(string reviewId, string userId, int rating, string comment);
}
