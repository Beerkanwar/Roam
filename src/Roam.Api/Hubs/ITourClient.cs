using System.Threading.Tasks;

namespace Roam.Api.Hubs;

public interface ITourClient
{
    Task ParticipantJoined(string connectionId);
    Task ParticipantLeft(string connectionId);
    Task ReceiveOffer(string senderConnectionId, string sdp);
    Task ReceiveAnswer(string senderConnectionId, string sdp);
    Task ReceiveIceCandidate(string senderConnectionId, string candidate);
    
    // Lifecycle events
    Task TourStarting(string sessionId);
    Task TourEnded(string sessionId);
    Task TourPaused(string sessionId);
    Task TourResumed(string sessionId);
}
