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

    public bool CanTransitionTo(TourRequestStatus targetState)
    {
        return targetState switch
        {
            TourRequestStatus.Published => Status == TourRequestStatus.Draft,
            TourRequestStatus.VolunteerInterested => Status == TourRequestStatus.Published || Status == TourRequestStatus.Matching,
            TourRequestStatus.Accepted => Status == TourRequestStatus.VolunteerInterested || Status == TourRequestStatus.Published || Status == TourRequestStatus.Matching,
            TourRequestStatus.Confirmed => Status == TourRequestStatus.Accepted,
            TourRequestStatus.Active => (Mode == SchedulingMode.Scheduled && Status == TourRequestStatus.Confirmed) ||
                                       (Mode == SchedulingMode.Instant && (Status == TourRequestStatus.Accepted || Status == TourRequestStatus.Confirmed)),
            TourRequestStatus.Completed => Status == TourRequestStatus.Active,
            TourRequestStatus.Cancelled => Status != TourRequestStatus.Completed && Status != TourRequestStatus.Active && Status != TourRequestStatus.Cancelled,
            TourRequestStatus.Expired => Status == TourRequestStatus.Published || Status == TourRequestStatus.Matching || Status == TourRequestStatus.VolunteerInterested,
            _ => false
        };
    }

    public void TransitionTo(TourRequestStatus targetState)
    {
        if (!CanTransitionTo(targetState))
            throw new InvalidOperationException($"Cannot transition from {Status} to {targetState}");

        Status = targetState;
    }

    /// <summary>
    /// True while a volunteer is attached to the request but the tour has not started, i.e. the
    /// volunteer can still step away and the request can return to discovery.
    /// </summary>
    public bool CanReleaseVolunteer =>
        !string.IsNullOrEmpty(AssignedVolunteerId) &&
        Status is TourRequestStatus.VolunteerInterested or TourRequestStatus.Accepted or TourRequestStatus.Confirmed;

    /// <summary>
    /// Detaches the assigned volunteer (they withdrew or their account was deleted) and puts the
    /// request back into discovery so another volunteer can pick it up. Any agreed start time was
    /// negotiated with the departing volunteer, so it is cleared.
    /// </summary>
    public void ReleaseVolunteer()
    {
        if (!CanReleaseVolunteer)
            throw new InvalidOperationException($"Cannot release the volunteer from a tour request in status {Status}.");

        Status = TourRequestStatus.Published;
        AssignedVolunteerId = null;
        RequestedStartTime = null;
    }
}
