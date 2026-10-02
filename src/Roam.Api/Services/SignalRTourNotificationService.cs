using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Roam.Api.Hubs;
using Roam.Application.TourRequests;

namespace Roam.Api.Services;

public class SignalRTourNotificationService : ITourNotificationService
{
    private readonly IHubContext<NotificationHub, INotificationClient> _notificationHub;
    private readonly IHubContext<TourHub, ITourClient> _tourHub;

    public SignalRTourNotificationService(
        IHubContext<NotificationHub, INotificationClient> notificationHub,
        IHubContext<TourHub, ITourClient> tourHub)
    {
        _notificationHub = notificationHub;
        _tourHub = tourHub;
    }

    public async Task TourRequestCreatedAsync(string requestId)
    {
        await _notificationHub.Clients.All.TourRequestCreated(requestId);
    }

    public async Task TourRequestAcceptedAsync(string requestId, string volunteerId, string requesterId)
    {
        await _notificationHub.Clients.User(requesterId).TourRequestAccepted(requestId, volunteerId);
    }

    public async Task TourStartingAsync(string requestId)
    {
        await _notificationHub.Clients.All.TourStarting(requestId);
        await _tourHub.Clients.Group(requestId).TourStarting(requestId);
    }

    public async Task TourEndedAsync(string requestId)
    {
        await _notificationHub.Clients.All.TourEnded(requestId);
        await _tourHub.Clients.Group(requestId).TourEnded(requestId);
    }
}
