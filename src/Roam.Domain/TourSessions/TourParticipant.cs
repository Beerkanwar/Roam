namespace Roam.Domain.TourSessions;

public class TourParticipant
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TourSessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public ParticipantRole Role { get; set; }
    
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LeftAt { get; set; }
    
    public string ConsentFlags { get; set; } = string.Empty;
}
