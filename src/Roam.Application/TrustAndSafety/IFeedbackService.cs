using System.Threading.Tasks;
using Roam.Domain.TrustAndSafety;

namespace Roam.Application.TrustAndSafety;

public interface IFeedbackService
{
    Task<Feedback> SubmitFeedbackAsync(string tourSessionId, string reviewerId, string targetUserId, FeedbackRating rating, string? comments);
}
