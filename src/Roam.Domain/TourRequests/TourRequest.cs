using Roam.Domain.Places;

namespace Roam.Domain.TourRequests;

public class TourRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string RequesterId { get; set; } = string.Empty;
    public string PlaceId { get; set; } = string.Empty;
    
    public VisibilityMode Visibility { get; set; }
    public SchedulingMode Mode { get; set; }
    
    public DateTime? RequestedStartTime { get; set; }
    public string Description { get; set; } = string.Empty;
    
    public TourRequestStatus Status { get; set; } = TourRequestStatus.Draft;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    
    // Navigation properties (if needed by EF)
    public Place? Place { get; set; }
}
