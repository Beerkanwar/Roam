using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Roam.Application.Chat;
using Roam.Contracts.Chat;
using Roam.Domain.Chat;
using Roam.Infrastructure.Persistence;

namespace Roam.Infrastructure.Chat;

public class ChatService : IChatService
{
    private readonly ApplicationDbContext _context;

    public ChatService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsUserParticipantAsync(string sessionId, string userId, CancellationToken cancellationToken = default)
    {
        // First check if the user is the host
        var session = await _context.TourSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
            
        if (session == null)
            return false;
            
        if (session.HostVolunteerId == userId)
            return true;
            
        // Then check if the user is a participant
        var isParticipant = await _context.TourParticipants
            .AnyAsync(p => p.TourSessionId == sessionId && p.UserId == userId, cancellationToken);
            
        return isParticipant;
    }

    public async Task<ChatMessageDto> SendMessageAsync(string sessionId, string senderId, string text, CancellationToken cancellationToken = default)
    {
        var isParticipant = await IsUserParticipantAsync(sessionId, senderId, cancellationToken);
        if (!isParticipant)
        {
            throw new UnauthorizedAccessException("User is not a participant in this session.");
        }

        var message = new ChatMessage
        {
            SessionId = sessionId,
            SenderId = senderId,
            Text = text,
            SentAt = DateTime.UtcNow
        };

        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);

        // Fetch sender name
        var senderName = await _context.Users
            .Where(u => u.Id == senderId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);

        return new ChatMessageDto
        {
            Id = message.Id,
            SessionId = message.SessionId,
            SenderId = message.SenderId,
            SenderName = senderName ?? "Unknown",
            Text = message.Text,
            SentAt = message.SentAt
        };
    }

    public async Task<IEnumerable<ChatMessageDto>> GetSessionChatHistoryAsync(string sessionId, string userId, CancellationToken cancellationToken = default)
    {
        var isParticipant = await IsUserParticipantAsync(sessionId, userId, cancellationToken);
        if (!isParticipant)
        {
            throw new UnauthorizedAccessException("User is not a participant in this session.");
        }

        var messages = await _context.ChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.SentAt)
            .Join(_context.Users, 
                m => m.SenderId, 
                u => u.Id, 
                (m, u) => new ChatMessageDto
                {
                    Id = m.Id,
                    SessionId = m.SessionId,
                    SenderId = m.SenderId,
                    SenderName = u.DisplayName,
                    Text = m.Text,
                    SentAt = m.SentAt
                })
            .ToListAsync(cancellationToken);

        return messages;
    }
}
