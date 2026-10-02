using System.Threading.Tasks;

namespace Roam.Api.Hubs;

public interface IChatClient
{
    Task ChatMessageReceived(string senderId, string message, string timestamp);
    Task MessageDeleted(string messageId);
    Task TypingStarted(string userId);
    Task TypingStopped(string userId);
}
