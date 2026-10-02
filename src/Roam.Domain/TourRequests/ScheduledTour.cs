using System;

namespace Roam.Domain.TourRequests;

public class ScheduledTour
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TourRequestId { get; set; } = string.Empty;
    public string ProposedByUserId { get; set; } = string.Empty;
    
    public DateTime ProposedStartUtc { get; set; }
    public DateTime? ProposedEndUtc { get; set; }
    
    public DateTime? RequesterAcceptedAt { get; set; }
    public DateTime? VolunteerAcceptedAt { get; set; }
    
    public ScheduledTourStatus Status { get; set; } = ScheduledTourStatus.Proposed;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Cancellation data for the proposal itself
    public string? CancelledByUserId { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? CancelledAt { get; set; }
}
