using System.Threading;
using System.Threading.Tasks;

namespace CinemaSystem.Application.Interfaces;

using CinemaSystem.Application.Common;

public class AiModerationResult
{
    public string Status { get; set; } = ReviewConstants.Flagged;
    public bool IsSpam { get; set; } = false;
    public string Reason { get; set; } = string.Empty;
    public string ModeratorMessage { get; set; } = string.Empty;
}

public interface IAiModerationService
{
    Task<AiModerationResult> ModerateReviewAsync(int starRating, string reviewText, CancellationToken cancellationToken = default);
}
