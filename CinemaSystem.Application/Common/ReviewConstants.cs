namespace CinemaSystem.Application.Common;

using CinemaSystem.Domain.Constants;

public static class ReviewConstants
{
    public const string Pending = DomainConstants.ReviewStatus.Pending;
    public const string Approved = DomainConstants.ReviewStatus.Approved;
    public const string Rejected = DomainConstants.ReviewStatus.Rejected;
    public const string Flagged = DomainConstants.ReviewStatus.Flagged;
}
