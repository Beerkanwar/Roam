using System.Threading.Tasks;

namespace Roam.Application.TourRequests;

public interface ITourNotificationService
{
    Task TourRequestCreatedAsync(string requestId);
    Task TourRequestAcceptedAsync(string requestId, string volunteerId, string requesterId);
    Task TourStartingAsync(string sessionId, string volunteerId, string requesterId);
    Task TourEndedAsync(string sessionId, string volunteerId, string requesterId);
    Task TourRequestCancelledAsync(string requestId, string cancelledByUserId, string counterpartId);
    Task TourRequestExpiredAsync(string requestId, string requesterId);
}
