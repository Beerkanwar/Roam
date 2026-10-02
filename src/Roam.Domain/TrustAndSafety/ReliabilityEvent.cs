namespace Roam.Domain.TrustAndSafety;

public class ReliabilityEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public ReliabilityEventType Type { get; set; }
    public string RelatedSessionId { get; set; } = string.Empty;
    public int Weight { get; set; } = 1; // Example weighting (e.g. 1 point for complete, -5 for no-show)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
