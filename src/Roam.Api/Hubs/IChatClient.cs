using System.Threading.Tasks;
using Roam.Contracts.Chat;

namespace Roam.Api.Hubs;

public interface IChatClient
{
    Task ChatMessageReceived(ChatMessageDto message);
    Task MessageDeleted(string messageId);
    Task TypingStarted(string userId);
    Task TypingStopped(string userId);
}
