using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Roam.Domain.TourSessions;
using Roam.Infrastructure.Persistence;

namespace Roam.Api.Hubs;

[Authorize]
public class TourHub : Hub<ITourClient>
{
    private static readonly ConcurrentDictionary<string, (string SessionId, string UserId)> SessionConnections = new();
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;

    public TourHub(ApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    private static string GetGroupName(string sessionId) => $"tour:{sessionId}";

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        SessionConnections.TryRemove(Context.ConnectionId, out _);
        await base.OnDisconnectedAsync(exception);
    }

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

        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null || user.AgeCategory != Domain.Users.AgeCategory.Adult)
            throw new HubException("Only age-verified adult users (18+) may participate in tour sessions.");

        if (session.Status != SessionStatus.Active && session.Status != SessionStatus.Starting)
            throw new HubException("Tour session is not currently active.");

        bool isMember = session.HostVolunteerId == userId || session.Participants.Any(p => p.UserId == userId);
        if (!isMember)
            throw new HubException("User is not authorized for this tour session.");

        return session;
    }

    private async Task EnsureSessionMembershipAsync(string sessionId)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            throw new HubException("Unauthorized.");

        // Use a short 15-second sliding cache to prevent hammering DB on ICE bursts,
        // while ensuring expired sessions or changed membership revoke access promptly.
        var cacheKey = $"tour_hub_session_valid_{sessionId}_{userId}";
        if (!_cache.TryGetValue(cacheKey, out bool isValid) || !isValid)
        {
            await ValidateSessionMembershipAsync(sessionId);
            _cache.Set(cacheKey, true, TimeSpan.FromSeconds(15));
        }
    }

    private async Task ValidateTargetConnectionAsync(string sessionId, string targetConnectionId)
    {
        if (!SessionConnections.TryGetValue(targetConnectionId, out var targetInfo) || targetInfo.SessionId != sessionId)
        {
            throw new HubException("Target connection is not a member of this active session.");
        }

        var senderId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(senderId) && !string.IsNullOrEmpty(targetInfo.UserId))
        {
            var isBlocked = await _context.UserBlocks.AnyAsync(b =>
                (b.BlockerId == senderId && b.BlockedId == targetInfo.UserId) ||
                (b.BlockerId == targetInfo.UserId && b.BlockedId == senderId));

            if (isBlocked)
            {
                throw new HubException("Signaling with a blocked user is not allowed.");
            }
        }
    }

    public async Task JoinSession(string sessionId)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        await ValidateSessionMembershipAsync(sessionId);
        
        SessionConnections[Context.ConnectionId] = (sessionId, userId);
        var groupName = GetGroupName(sessionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await Clients.Group(groupName).ParticipantJoined(Context.ConnectionId);
    }

    public async Task LeaveSession(string sessionId)
    {
        SessionConnections.TryRemove(Context.ConnectionId, out _);
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
            await ValidateTargetConnectionAsync(sessionId, targetConnectionId);
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
            await ValidateTargetConnectionAsync(sessionId, targetConnectionId);
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
            await ValidateTargetConnectionAsync(sessionId, targetConnectionId);
            await Clients.Client(targetConnectionId).ReceiveIceCandidate(Context.ConnectionId, candidate);
        }
        else
        {
            var groupName = GetGroupName(sessionId);
            await Clients.OthersInGroup(groupName).ReceiveIceCandidate(Context.ConnectionId, candidate);
        }
    }
}
