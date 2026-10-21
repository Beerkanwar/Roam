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
        // Target active volunteer discovery group
        await _notificationHub.Clients.Group("volunteers").TourRequestCreated(requestId);
    }

    public async Task TourRequestAcceptedAsync(string requestId, string volunteerId, string requesterId)
    {
        // Target only the requester
        await _notificationHub.Clients.User(requesterId).TourRequestAccepted(requestId, volunteerId);
    }

    public async Task TourStartingAsync(string sessionId, string volunteerId, string requesterId)
    {
        // Notify both parties directly via their user IDs with the sessionId
        await _notificationHub.Clients.User(requesterId).TourStarting(sessionId);
        await _notificationHub.Clients.User(volunteerId).TourStarting(sessionId);
        
        // Also notify in-room group if any clients joined early
        await _tourHub.Clients.Group($"tour:{sessionId}").TourStarting(sessionId);
    }

    public async Task TourEndedAsync(string sessionId, string volunteerId, string requesterId)
    {
        await _notificationHub.Clients.User(requesterId).TourEnded(sessionId);
        await _notificationHub.Clients.User(volunteerId).TourEnded(sessionId);
        await _tourHub.Clients.Group($"tour:{sessionId}").TourEnded(sessionId);
    }
}
