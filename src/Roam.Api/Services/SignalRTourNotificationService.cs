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
        // Target active volunteer discovery group instead of broadcasting to all clients
        await _notificationHub.Clients.Group("volunteers").TourRequestCreated(requestId);
    }

    public async Task TourRequestAcceptedAsync(string requestId, string volunteerId, string requesterId)
    {
        // Target only the requester
        await _notificationHub.Clients.User(requesterId).TourRequestAccepted(requestId, volunteerId);
    }

    public async Task TourStartingAsync(string requestId)
    {
        // Target only the specific tour session room group
        await _tourHub.Clients.Group($"tour:{requestId}").TourStarting(requestId);
    }

    public async Task TourEndedAsync(string requestId)
    {
        // Target only the specific tour session room group
        await _tourHub.Clients.Group($"tour:{requestId}").TourEnded(requestId);
    }
}
