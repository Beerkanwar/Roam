using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Roam.Api.Hubs;

[Authorize]
public class ChatHub : Hub<IChatClient>
{
    public async Task JoinSessionChat(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
    }

    public async Task LeaveSessionChat(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
    }

    public async Task SendMessage(string sessionId, string message)
    {
        var senderId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Unknown";
        var timestamp = DateTime.UtcNow.ToString("o");
        await Clients.Group(sessionId).ChatMessageReceived(senderId, message, timestamp);
    }

    public async Task SendTypingStarted(string sessionId)
    {
        var senderId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Unknown";
        await Clients.OthersInGroup(sessionId).TypingStarted(senderId);
    }

    public async Task SendTypingStopped(string sessionId)
    {
        var senderId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Unknown";
        await Clients.OthersInGroup(sessionId).TypingStopped(senderId);
    }
}
