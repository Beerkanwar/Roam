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
}
