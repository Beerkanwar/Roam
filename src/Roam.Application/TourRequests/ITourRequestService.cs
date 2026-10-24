using Roam.Domain.TourRequests;

namespace Roam.Application.TourRequests;

public class CreateTourRequestDto
{
    public string PlaceId { get; set; } = string.Empty;
    public string RequesterId { get; set; } = string.Empty;
    public VisibilityMode Visibility { get; set; }
    public SchedulingMode Mode { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class CreateTourRequestByCoordinatesDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string RequesterId { get; set; } = string.Empty;
    public VisibilityMode Visibility { get; set; }
    public SchedulingMode Mode { get; set; }
    public string Description { get; set; } = string.Empty;

    public bool Validate(out string? errorMessage)
    {
        if (double.IsNaN(Latitude) || Latitude is < -90 or > 90)
        {
            errorMessage = "Latitude must be between -90 and 90.";
            return false;
        }
        if (double.IsNaN(Longitude) || Longitude is < -180 or > 180)
        {
            errorMessage = "Longitude must be between -180 and 180.";
            return false;
        }
        errorMessage = null;
        return true;
    }
}

public class TourRequestResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string RequesterId { get; set; } = string.Empty;
    public string? PlaceId { get; set; }
    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public int Visibility { get; set; }
    public int Mode { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? AssignedVolunteerId { get; set; }
}

public interface ITourRequestService
{
    Task<TourRequest> CreateRequestAsync(CreateTourRequestDto dto, CancellationToken cancellationToken = default);
    Task<TourRequest> CreateRequestByCoordinatesAsync(CreateTourRequestByCoordinatesDto dto, CancellationToken cancellationToken = default);
    Task<bool> AcceptRequestAsync(string id, string volunteerId, CancellationToken cancellationToken = default);
    Task<ScheduledTour> ProposeScheduleAsync(string requestId, string userId, DateTime startTime, DateTime? endTime = null, CancellationToken cancellationToken = default);
    Task<bool> AcceptScheduleAsync(string scheduleId, string userId, CancellationToken cancellationToken = default);
    Task<bool> RejectScheduleAsync(string scheduleId, string userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TourRequest>> GetNearbyRequestsAsync(string volunteerId, double radiusMeters, CancellationToken cancellationToken = default);
    
    // Lifecycle Transitions
    Task<bool> CancelRequestAsync(string id, string userId, string reason, CancellationToken cancellationToken = default);
    Task<bool> ExpireRequestAsync(string id, CancellationToken cancellationToken = default);
    Task<bool> MarkVolunteerInterestedAsync(string id, string volunteerId, CancellationToken cancellationToken = default);
    Task<bool> StartTourAsync(string id, string volunteerId, CancellationToken cancellationToken = default);
    Task<bool> CompleteTourAsync(string id, string userId, CancellationToken cancellationToken = default);
}
