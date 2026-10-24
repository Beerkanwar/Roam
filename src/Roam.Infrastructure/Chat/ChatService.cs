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
        var isAdult = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.AgeCategory == Domain.Users.AgeCategory.Adult)
            .FirstOrDefaultAsync(cancellationToken);

        if (!isAdult) return false;

        var session = await _context.TourSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
            
        if (session == null)
            return false;
            
        if (session.HostVolunteerId == userId)
            return true;
            
        var isParticipant = await _context.TourParticipants
            .AnyAsync(p => p.TourSessionId == sessionId && p.UserId == userId, cancellationToken);
            
        return isParticipant;
    }

    public async Task<ChatMessageDto> SendMessageAsync(string sessionId, string senderId, string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Message text cannot be empty.", nameof(text));

        var trimmedText = text.Trim();
        if (trimmedText.Length > 2000)
            throw new ArgumentException("Message text cannot exceed 2000 characters.", nameof(text));

        var senderUser = await _context.Users.FindAsync(new object[] { senderId }, cancellationToken);
        if (senderUser == null || senderUser.AgeCategory != Domain.Users.AgeCategory.Adult)
        {
            throw new UnauthorizedAccessException("Only age-verified adult users (18+) may participate in chat.");
        }

        // 1. Fetch session with status, host, and participants in a single query
        var session = await _context.TourSessions
            .Include(s => s.Participants)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session == null)
            throw new ArgumentException("Tour session not found.", nameof(sessionId));

        if (session.Status != Domain.TourSessions.SessionStatus.Active)
            throw new InvalidOperationException("Cannot send messages to an inactive or completed session.");

        bool isParticipant = session.HostVolunteerId == senderId || session.Participants.Any(p => p.UserId == senderId);
        if (!isParticipant)
        {
            throw new UnauthorizedAccessException("User is not a participant in this session.");
        }

        // 2. Check if sender is blocked by or has blocked any other session participant
        var otherParticipantIds = session.Participants
            .Select(p => p.UserId)
            .Append(session.HostVolunteerId)
            .Where(id => id != senderId)
            .Distinct()
            .ToList();

        if (otherParticipantIds.Count > 0)
        {
            var isBlocked = await _context.UserBlocks.AnyAsync(b =>
                (b.BlockerId == senderId && otherParticipantIds.Contains(b.BlockedId)) ||
                (otherParticipantIds.Contains(b.BlockerId) && b.BlockedId == senderId), cancellationToken);

            if (isBlocked)
                throw new InvalidOperationException("Cannot send messages in a session containing blocked users.");
        }

        var message = new ChatMessage
        {
            SessionId = sessionId,
            SenderId = senderId,
            Text = trimmedText,
            SentAt = DateTime.UtcNow
        };

        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);

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

    public async Task<IEnumerable<ChatMessageDto>> GetSessionChatHistoryAsync(
        string sessionId, 
        string userId, 
        DateTime? before = null, 
        int limit = 50, 
        CancellationToken cancellationToken = default)
    {
        var isParticipant = await IsUserParticipantAsync(sessionId, userId, cancellationToken);
        if (!isParticipant)
        {
            throw new UnauthorizedAccessException("User is not a participant in this session.");
        }

        var pageLimit = Math.Clamp(limit, 1, 100);

        var blockedUserIds = await _context.UserBlocks
            .Where(b => b.BlockerId == userId || b.BlockedId == userId)
            .Select(b => b.BlockerId == userId ? b.BlockedId : b.BlockerId)
            .ToListAsync(cancellationToken);

        var query = _context.ChatMessages
            .Where(m => m.SessionId == sessionId);

        if (blockedUserIds.Count > 0)
        {
            query = query.Where(m => !blockedUserIds.Contains(m.SenderId));
        }

        if (before.HasValue)
        {
            query = query.Where(m => m.SentAt < before.Value);
        }

        var messages = await query
            .OrderByDescending(m => m.SentAt)
            .Take(pageLimit)
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

        // Return in chronological order
        messages.Reverse();
        return messages;
    }
}
