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
    
    public string? AssignedVolunteerId { get; set; }
    
    // Cancellation Data
    public string? CancelledByUserId { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? CancelledAt { get; set; }
    public bool WasLateCancellation { get; set; }
    
    // Navigation properties (if needed by EF)
    public Place? Place { get; set; }

    public void TransitionTo(TourRequestStatus targetState)
    {
        bool valid = targetState switch
        {
            TourRequestStatus.Published => Status == TourRequestStatus.Draft,
            TourRequestStatus.VolunteerInterested => Status == TourRequestStatus.Published || Status == TourRequestStatus.Matching,
            TourRequestStatus.Accepted => Status == TourRequestStatus.VolunteerInterested || Status == TourRequestStatus.Published || Status == TourRequestStatus.Matching,
            TourRequestStatus.Confirmed => Status == TourRequestStatus.Accepted,
            TourRequestStatus.Active => Status == TourRequestStatus.Confirmed || Status == TourRequestStatus.Accepted,
            TourRequestStatus.Completed => Status == TourRequestStatus.Active,
            TourRequestStatus.Cancelled => Status != TourRequestStatus.Completed && Status != TourRequestStatus.Active && Status != TourRequestStatus.Cancelled,
            TourRequestStatus.Expired => Status == TourRequestStatus.Published || Status == TourRequestStatus.Matching,
            _ => false
        };

        if (!valid)
            throw new InvalidOperationException($"Cannot transition from {Status} to {targetState}");

        Status = targetState;
    }
}
