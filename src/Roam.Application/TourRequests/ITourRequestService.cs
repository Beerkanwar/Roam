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
}

public interface ITourRequestService
{
    Task<TourRequest> CreateRequestAsync(CreateTourRequestDto dto, CancellationToken cancellationToken = default);
    Task<TourRequest> CreateRequestByCoordinatesAsync(CreateTourRequestByCoordinatesDto dto, CancellationToken cancellationToken = default);
    Task<bool> AcceptRequestAsync(string id, string volunteerId, CancellationToken cancellationToken = default);
    Task<bool> ScheduleRequestAsync(string id, DateTime scheduledTime, CancellationToken cancellationToken = default);
    Task<IEnumerable<TourRequest>> GetNearbyRequestsAsync(string volunteerId, double radiusMeters, CancellationToken cancellationToken = default);
}
