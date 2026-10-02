using System;
using System.Threading.Tasks;
using Roam.Domain.TrustAndSafety;

namespace Roam.Application.TrustAndSafety;

public interface IFeedbackService
{
    Task<Feedback> SubmitFeedbackAsync(Guid tourSessionId, Guid reviewerId, Guid targetUserId, FeedbackRating rating, string? comments);
}
