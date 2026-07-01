using System.Threading;
using System.Threading.Tasks;

namespace CinemaSystem.Application.Interfaces;

public class AiModerationResult
{
    public string Status { get; set; } = "FLAGGED"; 
    public bool IsSpam { get; set; } = false;
    public string Reason { get; set; } = string.Empty;
    public string ModeratorMessage { get; set; } = string.Empty;
}

public interface IAiModerationService
{
    Task<AiModerationResult> ModerateReviewAsync(int starRating, string reviewText, CancellationToken cancellationToken = default);
}
