using System.Threading.Tasks;

namespace Roam.Application.TourRequests;

public interface ITourNotificationService
{
    Task TourRequestCreatedAsync(string requestId);
    Task TourRequestAcceptedAsync(string requestId, string volunteerId, string requesterId);
    Task TourStartingAsync(string requestId);
    Task TourEndedAsync(string requestId);
}
