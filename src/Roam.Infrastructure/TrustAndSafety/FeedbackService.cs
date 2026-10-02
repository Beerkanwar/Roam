using System;
using System.Threading.Tasks;
using Roam.Application.TrustAndSafety;
using Roam.Domain.TrustAndSafety;
using Roam.Infrastructure.Persistence;

namespace Roam.Infrastructure.TrustAndSafety;

public class FeedbackService : IFeedbackService
{
    private readonly ApplicationDbContext _context;

    public FeedbackService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Feedback> SubmitFeedbackAsync(Guid tourSessionId, Guid reviewerId, Guid targetUserId, FeedbackRating rating, string? comments)
    {
        var feedback = new Feedback(tourSessionId, reviewerId, targetUserId, rating, comments);
        _context.Feedbacks.Add(feedback);

        // Map feedback to a reliability event (stub logic for MVP)
        int scoreDelta = rating switch
        {
            FeedbackRating.Positive => +10,
            FeedbackRating.Neutral => 0,
            FeedbackRating.Negative => -10,
            _ => 0
        };

        if (scoreDelta != 0)
        {
            var reliabilityEvent = new ReliabilityEvent
            {
                UserId = targetUserId.ToString(),
                Type = rating == FeedbackRating.Negative ? ReliabilityEventType.ModerationViolation : ReliabilityEventType.CompletedTour,
                RelatedSessionId = tourSessionId.ToString(),
                Weight = scoreDelta
            };
            _context.ReliabilityEvents.Add(reliabilityEvent);
        }

        await _context.SaveChangesAsync();
        return feedback;
    }
}
