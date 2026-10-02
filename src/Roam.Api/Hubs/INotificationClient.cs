using System.Threading.Tasks;

namespace Roam.Api.Hubs;

public interface INotificationClient
{
    Task TourRequestCreated(string requestId);
    Task TourRequestMatched(string requestId);
    Task TourRequestAccepted(string requestId, string volunteerId);
    Task ScheduleProposed(string requestId, string proposedTime);
    Task ScheduleAccepted(string requestId);
    Task TourStarting(string requestId);
    Task TourEnded(string requestId);
    Task ReportStatusChanged(string reportId, string status);
}
