using System;

namespace Roam.Domain.TrustAndSafety;

public class Feedback
{
    public Guid Id { get; private set; }
    public Guid TourSessionId { get; private set; }
    public Guid ReviewerId { get; private set; }
    public Guid TargetUserId { get; private set; }
    public FeedbackRating Rating { get; private set; }
    public string? Comments { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Feedback() { } // EF Core

    public Feedback(Guid tourSessionId, Guid reviewerId, Guid targetUserId, FeedbackRating rating, string? comments = null)
    {
        Id = Guid.NewGuid();
        TourSessionId = tourSessionId;
        ReviewerId = reviewerId;
        TargetUserId = targetUserId;
        Rating = rating;
        Comments = comments;
        CreatedAt = DateTime.UtcNow;
    }
}
