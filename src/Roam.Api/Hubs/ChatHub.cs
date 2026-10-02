using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Roam.Api.Hubs;

using Roam.Application.Chat;
using Roam.Contracts.Chat;

[Authorize]
public class ChatHub : Hub<IChatClient>
{
    private readonly IChatService _chatService;

    public ChatHub(IChatService chatService)
    {
        _chatService = chatService;
    }

    public async Task JoinSessionChat(string sessionId)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId) || !await _chatService.IsUserParticipantAsync(sessionId, userId))
        {
            throw new HubException("Unauthorized to join this session's chat.");
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
    }

    public async Task LeaveSessionChat(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
    }

    public async Task SendMessage(string sessionId, string message)
    {
        var senderId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(senderId))
        {
            throw new HubException("Unauthorized.");
        }

        var savedMessageDto = await _chatService.SendMessageAsync(sessionId, senderId, message);
        await Clients.Group(sessionId).ChatMessageReceived(savedMessageDto);
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
