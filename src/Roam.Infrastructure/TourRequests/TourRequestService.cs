using Microsoft.EntityFrameworkCore;
using Roam.Application.TourRequests;
using Roam.Domain.TourRequests;
using Roam.Infrastructure.Persistence;

namespace Roam.Infrastructure.TourRequests;

public class TourRequestService : ITourRequestService
{
    private readonly ApplicationDbContext _context;
    private readonly ITourNotificationService _notificationService;

    public TourRequestService(
        ApplicationDbContext context, 
        ITourNotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<TourRequest> CreateRequestAsync(CreateTourRequestDto dto, CancellationToken cancellationToken = default)
    {
        var request = new TourRequest
        {
            RequesterId = dto.RequesterId,
            PlaceId = dto.PlaceId,
            Visibility = dto.Visibility,
            Mode = dto.Mode,
            Description = dto.Description,
            Status = TourRequestStatus.Published // Implicitly moves from Draft -> Published upon creation for MVP
        };

        _context.TourRequests.Add(request);
        await _context.SaveChangesAsync(cancellationToken);

        await _notificationService.TourRequestCreatedAsync(request.Id);

        return request;
    }

    public async Task<TourRequest> CreateRequestByCoordinatesAsync(CreateTourRequestByCoordinatesDto dto, CancellationToken cancellationToken = default)
    {
        var place = new Roam.Domain.Places.Place
        {
            Name = "Custom Location",
            Description = "Requested at specific coordinates",
            Location = new NetTopologySuite.Geometries.Point(dto.Longitude, dto.Latitude) { SRID = 4326 }
        };

        _context.Places.Add(place);
        await _context.SaveChangesAsync(cancellationToken);

        var request = new TourRequest
        {
            RequesterId = dto.RequesterId,
            PlaceId = place.Id,
            Visibility = dto.Visibility,
            Mode = dto.Mode,
            Description = dto.Description,
            Status = TourRequestStatus.Published
        };

        _context.TourRequests.Add(request);
        await _context.SaveChangesAsync(cancellationToken);

        return request;
    }

    public async Task<bool> AcceptRequestAsync(string id, string volunteerId, CancellationToken cancellationToken = default)
    {
        var request = await _context.TourRequests.FindAsync(new object[] { id }, cancellationToken);
        if (request == null) return false;

        request.TransitionTo(TourRequestStatus.Accepted);
        request.AssignedVolunteerId = volunteerId;
        await _context.SaveChangesAsync(cancellationToken);

        // Notify the requester and global feed
        await _notificationService.TourRequestAcceptedAsync(id, volunteerId, request.RequesterId);
        
        return true;
    }

    public async Task<bool> ScheduleRequestAsync(string id, DateTime scheduledTime, CancellationToken cancellationToken = default)
    {
        var request = await _context.TourRequests.FindAsync(new object[] { id }, cancellationToken);
        if (request == null) return false;

        request.TransitionTo(TourRequestStatus.Confirmed);

        request.RequestedStartTime = scheduledTime;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CancelRequestAsync(string id, string userId, string reason, CancellationToken cancellationToken = default)
    {
        var request = await _context.TourRequests.FindAsync(new object[] { id }, cancellationToken);
        if (request == null) return false;

        if (request.RequesterId != userId && request.AssignedVolunteerId != userId)
            throw new UnauthorizedAccessException("Only the requester or assigned volunteer can cancel this tour.");

        request.TransitionTo(TourRequestStatus.Cancelled);

        request.CancelledByUserId = userId;
        request.CancellationReason = reason;
        request.CancelledAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ExpireRequestAsync(string id, CancellationToken cancellationToken = default)
    {
        var request = await _context.TourRequests.FindAsync(new object[] { id }, cancellationToken);
        if (request == null) return false;

        request.TransitionTo(TourRequestStatus.Expired);
        
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> MarkVolunteerInterestedAsync(string id, string volunteerId, CancellationToken cancellationToken = default)
    {
        var request = await _context.TourRequests.FindAsync(new object[] { id }, cancellationToken);
        if (request == null) return false;

        request.TransitionTo(TourRequestStatus.VolunteerInterested);
        
        request.AssignedVolunteerId = volunteerId; // tentative
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> StartTourAsync(string id, string volunteerId, CancellationToken cancellationToken = default)
    {
        var request = await _context.TourRequests.FindAsync(new object[] { id }, cancellationToken);
        if (request == null) return false;

        if (request.AssignedVolunteerId != volunteerId)
            throw new UnauthorizedAccessException("Only the assigned volunteer can start this tour.");

        request.TransitionTo(TourRequestStatus.Active);
        
        await _context.SaveChangesAsync(cancellationToken);

        // Notify global feed and session participants
        await _notificationService.TourStartingAsync(id);

        return true;
    }

    public async Task<bool> CompleteTourAsync(string id, string volunteerId, CancellationToken cancellationToken = default)
    {
        var request = await _context.TourRequests.FindAsync(new object[] { id }, cancellationToken);
        if (request == null) return false;

        if (request.AssignedVolunteerId != volunteerId)
            throw new UnauthorizedAccessException("Only the assigned volunteer can complete this tour.");

        request.TransitionTo(TourRequestStatus.Completed);
        
        await _context.SaveChangesAsync(cancellationToken);

        await _notificationService.TourEndedAsync(id);

        return true;
    }

    public async Task<IEnumerable<TourRequest>> GetNearbyRequestsAsync(string volunteerId, double radiusMeters, CancellationToken cancellationToken = default)
    {
        // 1. Get the volunteer's current location
        var userLocation = await _context.UserLocations
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.UserId == volunteerId, cancellationToken);

        if (userLocation == null)
        {
            return Array.Empty<TourRequest>(); // We cannot find nearby requests if we don't know where the volunteer is
        }

        // 2. Find published requests that are associated with a Place within the radius
        var nearbyRequests = await _context.TourRequests
            .Include(r => r.Place) // Ensure Place data is loaded
            .Where(r => r.Status == TourRequestStatus.Published)
            .Where(r => r.Place.Location.IsWithinDistance(userLocation.Location, radiusMeters))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return nearbyRequests;
    }
}
