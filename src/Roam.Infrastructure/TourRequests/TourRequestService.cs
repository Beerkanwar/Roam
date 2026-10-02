using Microsoft.EntityFrameworkCore;
using Roam.Application.TourRequests;
using Roam.Domain.TourRequests;
using Roam.Infrastructure.Persistence;

namespace Roam.Infrastructure.TourRequests;

public class TourRequestService : ITourRequestService
{
    private readonly ApplicationDbContext _context;

    public TourRequestService(ApplicationDbContext context)
    {
        _context = context;
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

        // Valid transitions to Accepted: VolunteerInterested or Published (if immediate accept is allowed)
        if (request.Status == TourRequestStatus.Published || request.Status == TourRequestStatus.Matching)
        {
            request.Status = TourRequestStatus.Accepted;
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        return false; // Invalid transition
    }

    public async Task<bool> ScheduleRequestAsync(string id, DateTime scheduledTime, CancellationToken cancellationToken = default)
    {
        var request = await _context.TourRequests.FindAsync(new object[] { id }, cancellationToken);
        if (request == null) return false;

        if (request.Status == TourRequestStatus.Accepted)
        {
            request.RequestedStartTime = scheduledTime;
            request.Status = TourRequestStatus.Confirmed;
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        return false;
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
