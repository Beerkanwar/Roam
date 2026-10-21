using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Roam.Contracts.Chat;

namespace Roam.Application.Chat;

public interface IChatService
{
    Task<ChatMessageDto> SendMessageAsync(string sessionId, string senderId, string text, CancellationToken cancellationToken = default);
    Task<IEnumerable<ChatMessageDto>> GetSessionChatHistoryAsync(string sessionId, string userId, DateTime? before = null, int limit = 50, CancellationToken cancellationToken = default);
    Task<bool> IsUserParticipantAsync(string sessionId, string userId, CancellationToken cancellationToken = default);
}
