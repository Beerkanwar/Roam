using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Roam.Application.TrustAndSafety;
using Roam.Domain.TourSessions;
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

    public async Task<Feedback> SubmitFeedbackAsync(string tourSessionId, string reviewerId, string targetUserId, FeedbackRating rating, string? comments)
    {
        if (reviewerId == targetUserId)
            throw new InvalidOperationException("Users cannot submit feedback on themselves.");

        // Verify session exists and is Completed
        var session = await _context.TourSessions
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.Id == tourSessionId);

        if (session == null)
            throw new ArgumentException("Tour session not found.");

        if (session.Status != SessionStatus.Completed)
            throw new InvalidOperationException("Feedback can only be submitted for completed sessions.");

        // Verify reviewer participated in the session
        bool isReviewerParticipant = session.HostVolunteerId == reviewerId || 
                                     session.Participants.Any(p => p.UserId == reviewerId);
        if (!isReviewerParticipant)
            throw new UnauthorizedAccessException("Reviewer was not a participant in this session.");

        // Verify target participated in the session as counterpart
        bool isTargetParticipant = session.HostVolunteerId == targetUserId || 
                                   session.Participants.Any(p => p.UserId == targetUserId);
        if (!isTargetParticipant)
            throw new InvalidOperationException("Target user was not a participant in this session.");

        // Check for blocks between users
        bool isBlocked = await _context.UserBlocks.AnyAsync(b =>
            (b.BlockerId == reviewerId && b.BlockedId == targetUserId) ||
            (b.BlockerId == targetUserId && b.BlockedId == reviewerId));
        if (isBlocked)
            throw new InvalidOperationException("Cannot submit feedback for a blocked user.");

        // Enforce uniqueness per (session, reviewer, target)
        bool alreadySubmitted = await _context.Feedbacks.AnyAsync(f =>
            f.TourSessionId == tourSessionId && f.ReviewerId == reviewerId && f.TargetUserId == targetUserId);
        if (alreadySubmitted)
            throw new InvalidOperationException("Feedback has already been submitted for this session.");

        var feedback = new Feedback(tourSessionId, reviewerId, targetUserId, rating, comments);
        _context.Feedbacks.Add(feedback);

        // Map feedback to a reliability event
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
                UserId = targetUserId,
                Type = rating == FeedbackRating.Negative ? ReliabilityEventType.ModerationViolation : ReliabilityEventType.CompletedTour,
                RelatedSessionId = tourSessionId,
                Weight = scoreDelta
            };
            _context.ReliabilityEvents.Add(reliabilityEvent);
        }

        await _context.SaveChangesAsync();
        return feedback;
    }
}
