using System;

namespace Roam.Domain.TrustAndSafety;

public class Feedback
{
    public string Id { get; private set; }
    public string TourSessionId { get; private set; }
    public string ReviewerId { get; private set; }
    public string TargetUserId { get; private set; }
    public FeedbackRating Rating { get; private set; }
    public string? Comments { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Feedback() 
    { 
        Id = Guid.NewGuid().ToString();
        TourSessionId = string.Empty;
        ReviewerId = string.Empty;
        TargetUserId = string.Empty;
    }

    public Feedback(string tourSessionId, string reviewerId, string targetUserId, FeedbackRating rating, string? comments = null)
    {
        Id = Guid.NewGuid().ToString();
        TourSessionId = tourSessionId;
        ReviewerId = reviewerId;
        TargetUserId = targetUserId;
        Rating = rating;
        Comments = comments;
        CreatedAt = DateTime.UtcNow;
    }
}
