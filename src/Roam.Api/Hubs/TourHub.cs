using Microsoft.AspNetCore.SignalR;

namespace Roam.Api.Hubs;

public class TourHub : Hub
{
    // Join a specific tour session room
    public async Task JoinSession(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
        await Clients.Group(sessionId).SendAsync("ParticipantJoined", Context.ConnectionId);
    }

    // Leave a specific tour session room
    public async Task LeaveSession(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
        await Clients.Group(sessionId).SendAsync("ParticipantLeft", Context.ConnectionId);
    }

    // WebRTC Signaling: SDP Offer
    public async Task SendOffer(string sessionId, string sdp)
    {
        // In a real implementation, we might target a specific peer. 
        // Here we broadcast to others in the group.
        await Clients.OthersInGroup(sessionId).SendAsync("ReceiveOffer", Context.ConnectionId, sdp);
    }

    // WebRTC Signaling: SDP Answer
    public async Task SendAnswer(string sessionId, string sdp)
    {
        await Clients.OthersInGroup(sessionId).SendAsync("ReceiveAnswer", Context.ConnectionId, sdp);
    }

    // WebRTC Signaling: ICE Candidate
    public async Task SendIceCandidate(string sessionId, string candidate)
    {
        await Clients.OthersInGroup(sessionId).SendAsync("ReceiveIceCandidate", Context.ConnectionId, candidate);
    }
}
