using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Roam.Domain.TourSessions;
using Roam.Infrastructure.Persistence;

namespace Roam.Api.Hubs;

[Authorize]
public class TourHub : Hub<ITourClient>
{
    private readonly ApplicationDbContext _context;

    public TourHub(ApplicationDbContext context)
    {
        _context = context;
    }

    private static string GetGroupName(string sessionId) => $"tour:{sessionId}";

    private async Task<TourSession> ValidateSessionMembershipAsync(string sessionId)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            throw new HubException("Unauthorized.");

        var session = await _context.TourSessions
            .Include(s => s.Participants)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
            throw new HubException("Tour session not found.");

        if (session.Status != SessionStatus.Active && session.Status != SessionStatus.Starting)
            throw new HubException("Tour session is not currently active.");

        bool isMember = session.HostVolunteerId == userId || session.Participants.Any(p => p.UserId == userId);
        if (!isMember)
            throw new HubException("User is not authorized for this tour session.");

        return session;
    }

    private async Task EnsureSessionMembershipAsync(string sessionId)
    {
        if (Context.Items.TryGetValue("ActiveSessionId", out var active) && active is string activeSessionId && activeSessionId == sessionId)
        {
            return;
        }

        await ValidateSessionMembershipAsync(sessionId);
        Context.Items["ActiveSessionId"] = sessionId;
    }

    public async Task JoinSession(string sessionId)
    {
        await ValidateSessionMembershipAsync(sessionId);
        Context.Items["ActiveSessionId"] = sessionId;
        var groupName = GetGroupName(sessionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await Clients.Group(groupName).ParticipantJoined(Context.ConnectionId);
    }

    public async Task LeaveSession(string sessionId)
    {
        Context.Items.Remove("ActiveSessionId");
        var groupName = GetGroupName(sessionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        await Clients.Group(groupName).ParticipantLeft(Context.ConnectionId);
    }

    public async Task SendOffer(string sessionId, string sdp, string? targetConnectionId = null)
    {
        if (string.IsNullOrWhiteSpace(sdp) || sdp.Length > 65536)
            throw new HubException("Invalid SDP payload.");

        await EnsureSessionMembershipAsync(sessionId);
        if (!string.IsNullOrEmpty(targetConnectionId))
        {
            await Clients.Client(targetConnectionId).ReceiveOffer(Context.ConnectionId, sdp);
        }
        else
        {
            var groupName = GetGroupName(sessionId);
            await Clients.OthersInGroup(groupName).ReceiveOffer(Context.ConnectionId, sdp);
        }
    }

    public async Task SendAnswer(string sessionId, string sdp, string? targetConnectionId = null)
    {
        if (string.IsNullOrWhiteSpace(sdp) || sdp.Length > 65536)
            throw new HubException("Invalid SDP payload.");

        await EnsureSessionMembershipAsync(sessionId);
        if (!string.IsNullOrEmpty(targetConnectionId))
        {
            await Clients.Client(targetConnectionId).ReceiveAnswer(Context.ConnectionId, sdp);
        }
        else
        {
            var groupName = GetGroupName(sessionId);
            await Clients.OthersInGroup(groupName).ReceiveAnswer(Context.ConnectionId, sdp);
        }
    }

    public async Task SendIceCandidate(string sessionId, string candidate, string? targetConnectionId = null)
    {
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > 8192)
            throw new HubException("Invalid ICE candidate payload.");

        await EnsureSessionMembershipAsync(sessionId);
        if (!string.IsNullOrEmpty(targetConnectionId))
        {
            await Clients.Client(targetConnectionId).ReceiveIceCandidate(Context.ConnectionId, candidate);
        }
        else
        {
            var groupName = GetGroupName(sessionId);
            await Clients.OthersInGroup(groupName).ReceiveIceCandidate(Context.ConnectionId, candidate);
        }
    }
}
