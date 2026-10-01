namespace Roam.Domain.TourSessions;

public class TourSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TourRequestId { get; set; } = string.Empty;
    public string HostVolunteerId { get; set; } = string.Empty;
    
    public SessionStatus Status { get; set; } = SessionStatus.Starting;
    
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public string EndReason { get; set; } = string.Empty;
    
    public int MaxParticipants { get; set; } = 10;
    
    public ICollection<TourParticipant> Participants { get; set; } = new List<TourParticipant>();
}
