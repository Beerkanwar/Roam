using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Roam.Application.TourRequests;
using Roam.Domain.Places;
using Roam.Domain.TourRequests;
using Roam.Domain.TourSessions;
using Roam.Domain.Users;
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
        var placeExists = await _context.Places.AnyAsync(p => p.Id == dto.PlaceId, cancellationToken);
        if (!placeExists)
            throw new ArgumentException("The specified place was not found.", nameof(dto.PlaceId));

        var expiresAt = dto.Mode == SchedulingMode.Instant 
            ? DateTime.UtcNow.AddHours(24) 
            : DateTime.UtcNow.AddDays(7);

        var request = new TourRequest
        {
            RequesterId = dto.RequesterId,
            PlaceId = dto.PlaceId,
            Visibility = dto.Visibility,
            Mode = dto.Mode,
            Description = dto.Description,
            Status = TourRequestStatus.Published,
            ExpiresAt = expiresAt
        };

        _context.TourRequests.Add(request);
        await _context.SaveChangesAsync(cancellationToken);

        await _notificationService.TourRequestCreatedAsync(request.Id);

        return request;
    }

    public async Task<TourRequest> CreateRequestByCoordinatesAsync(CreateTourRequestByCoordinatesDto dto, CancellationToken cancellationToken = default)
    {
        if (!dto.Validate(out var validationError))
            throw new ArgumentException(validationError, nameof(dto));

        var location = new NetTopologySuite.Geometries.Point(dto.Longitude, dto.Latitude) { SRID = 4326 };

        // Reuse an existing place within ~50 meters if one already exists
        var existingPlace = await _context.Places
            .Where(p => p.Location.IsWithinDistance(location, 0.0005))
            .FirstOrDefaultAsync(cancellationToken);

        string placeId;
        if (existingPlace != null)
        {
            placeId = existingPlace.Id;
        }
        else
        {
            var place = new Place
            {
                Name = $"Location ({dto.Latitude:F4}, {dto.Longitude:F4})",
                Description = "Custom requested location",
                Location = location
            };
            _context.Places.Add(place);
            await _context.SaveChangesAsync(cancellationToken);
            placeId = place.Id;
        }

        var expiresAt = dto.Mode == SchedulingMode.Instant 
            ? DateTime.UtcNow.AddHours(24) 
            : DateTime.UtcNow.AddDays(7);

        var request = new TourRequest
        {
            RequesterId = dto.RequesterId,
            PlaceId = placeId,
            Visibility = dto.Visibility,
            Mode = dto.Mode,
            Description = dto.Description,
            Status = TourRequestStatus.Published,
            ExpiresAt = expiresAt
        };

        _context.TourRequests.Add(request);
        await _context.SaveChangesAsync(cancellationToken);

        await _notificationService.TourRequestCreatedAsync(request.Id);

        return request;
    }

    public async Task<bool> AcceptRequestAsync(string id, string volunteerId, CancellationToken cancellationToken = default)
    {
        // 1. Volunteer verification
        var volunteer = await _context.Users.FindAsync(new object[] { volunteerId }, cancellationToken);
        if (volunteer == null || volunteer.VolunteerStatus != VolunteerStatus.Approved)
            throw new UnauthorizedAccessException("Only approved volunteers can accept tour requests.");

        var request = await _context.TourRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (request == null) return false;

        if (request.RequesterId == volunteerId)
            throw new InvalidOperationException("Volunteers cannot accept their own tour requests.");

        // 2. Concurrency & state preconditions
        if (request.Status != TourRequestStatus.Published && request.Status != TourRequestStatus.VolunteerInterested)
            throw new InvalidOperationException($"Cannot accept tour request in status {request.Status}.");

        if (!string.IsNullOrEmpty(request.AssignedVolunteerId) && request.AssignedVolunteerId != volunteerId)
            throw new InvalidOperationException("Request has already been assigned to another volunteer.");

        // 3. User block check
        var isBlocked = await _context.UserBlocks.AnyAsync(b =>
            (b.BlockerId == volunteerId && b.BlockedId == request.RequesterId) ||
            (b.BlockerId == request.RequesterId && b.BlockedId == volunteerId), cancellationToken);

        if (isBlocked)
            throw new InvalidOperationException("Cannot accept a tour request from a blocked user.");

        // 4. Atomic conditional update to prevent race conditions
        var rowsAffected = await _context.TourRequests
            .Where(r => r.Id == id && 
                       (r.Status == TourRequestStatus.Published || r.Status == TourRequestStatus.VolunteerInterested) && 
                       (r.AssignedVolunteerId == null || r.AssignedVolunteerId == volunteerId))
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, TourRequestStatus.Accepted)
                .SetProperty(r => r.AssignedVolunteerId, volunteerId), cancellationToken);

        if (rowsAffected == 0)
        {
            throw new InvalidOperationException("Tour request was already accepted by another volunteer.");
        }

        await _notificationService.TourRequestAcceptedAsync(id, volunteerId, request.RequesterId);
        
        return true;
    }

    public async Task<ScheduledTour> ProposeScheduleAsync(string requestId, string userId, DateTime startTime, DateTime? endTime = null, CancellationToken cancellationToken = default)
    {
        if (startTime < DateTime.UtcNow.AddMinutes(5))
            throw new ArgumentException("Proposed start time must be at least 5 minutes in the future.");

        if (endTime.HasValue && endTime.Value <= startTime)
            throw new ArgumentException("Proposed end time must be after the start time.");

        var request = await _context.TourRequests.FindAsync(new object[] { requestId }, cancellationToken);
        if (request == null) throw new InvalidOperationException("Request not found");

        if (request.RequesterId != userId && request.AssignedVolunteerId != userId)
            throw new UnauthorizedAccessException("User is not authorized to propose a schedule for this request.");

        var schedule = new ScheduledTour
        {
            TourRequestId = requestId,
            ProposedByUserId = userId,
            ProposedStartUtc = DateTime.SpecifyKind(startTime, DateTimeKind.Utc),
            ProposedEndUtc = endTime.HasValue ? DateTime.SpecifyKind(endTime.Value, DateTimeKind.Utc) : null
        };

        if (request.RequesterId == userId)
            schedule.RequesterAcceptedAt = DateTime.UtcNow;
        else
            schedule.VolunteerAcceptedAt = DateTime.UtcNow;

        _context.ScheduledTours.Add(schedule);
        await _context.SaveChangesAsync(cancellationToken);

        return schedule;
    }

    public async Task<bool> AcceptScheduleAsync(string scheduleId, string userId, CancellationToken cancellationToken = default)
    {
        var schedule = await _context.ScheduledTours.FindAsync(new object[] { scheduleId }, cancellationToken);
        if (schedule == null || schedule.Status != ScheduledTourStatus.Proposed) return false;

        var request = await _context.TourRequests.FindAsync(new object[] { schedule.TourRequestId }, cancellationToken);
        if (request == null) return false;

        if (request.RequesterId != userId && request.AssignedVolunteerId != userId)
            throw new UnauthorizedAccessException("User is not authorized to accept this schedule.");

        if (request.RequesterId == userId)
            schedule.RequesterAcceptedAt = DateTime.UtcNow;
        else
            schedule.VolunteerAcceptedAt = DateTime.UtcNow;

        if (schedule.RequesterAcceptedAt.HasValue && schedule.VolunteerAcceptedAt.HasValue)
        {
            schedule.Status = ScheduledTourStatus.Accepted;
            schedule.UpdatedAt = DateTime.UtcNow;
            
            request.TransitionTo(TourRequestStatus.Confirmed);
            request.RequestedStartTime = schedule.ProposedStartUtc;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RejectScheduleAsync(string scheduleId, string userId, CancellationToken cancellationToken = default)
    {
        var schedule = await _context.ScheduledTours.FindAsync(new object[] { scheduleId }, cancellationToken);
        if (schedule == null || schedule.Status != ScheduledTourStatus.Proposed) return false;

        var request = await _context.TourRequests.FindAsync(new object[] { schedule.TourRequestId }, cancellationToken);
        if (request == null) return false;

        if (request.RequesterId != userId && request.AssignedVolunteerId != userId)
            throw new UnauthorizedAccessException("User is not authorized to reject this schedule.");

        schedule.Status = ScheduledTourStatus.Rejected;
        schedule.UpdatedAt = DateTime.UtcNow;

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

        var counterpartId = request.RequesterId == userId 
            ? request.AssignedVolunteerId 
            : request.RequesterId;

        await _context.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrEmpty(counterpartId))
        {
            await _notificationService.TourRequestCancelledAsync(id, userId, counterpartId);
        }

        return true;
    }

    public async Task<bool> ExpireRequestAsync(string id, CancellationToken cancellationToken = default)
    {
        var request = await _context.TourRequests.FindAsync(new object[] { id }, cancellationToken);
        if (request == null) return false;

        request.TransitionTo(TourRequestStatus.Expired);
        var requesterId = request.RequesterId;
        
        await _context.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrEmpty(requesterId))
        {
            await _notificationService.TourRequestExpiredAsync(id, requesterId);
        }

        return true;
    }

    public async Task<bool> MarkVolunteerInterestedAsync(string id, string volunteerId, CancellationToken cancellationToken = default)
    {
        var volunteer = await _context.Users.FindAsync(new object[] { volunteerId }, cancellationToken);
        if (volunteer == null || volunteer.VolunteerStatus != VolunteerStatus.Approved)
            throw new UnauthorizedAccessException("Only approved volunteers can express interest in tours.");

        var request = await _context.TourRequests.FindAsync(new object[] { id }, cancellationToken);
        if (request == null) return false;

        if (request.RequesterId == volunteerId)
            throw new InvalidOperationException("Volunteers cannot express interest in their own tour requests.");

        var isBlocked = await _context.UserBlocks.AnyAsync(b =>
            (b.BlockerId == volunteerId && b.BlockedId == request.RequesterId) ||
            (b.BlockerId == request.RequesterId && b.BlockedId == volunteerId), cancellationToken);

        if (isBlocked)
            throw new InvalidOperationException("Cannot express interest in a tour request from a blocked user.");

        if (!string.IsNullOrEmpty(request.AssignedVolunteerId) && request.AssignedVolunteerId != volunteerId)
            throw new InvalidOperationException("Request has already been claimed by another volunteer.");

        request.TransitionTo(TourRequestStatus.VolunteerInterested);
        request.AssignedVolunteerId = volunteerId;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> StartTourAsync(string id, string volunteerId, CancellationToken cancellationToken = default)
    {
        var volunteer = await _context.Users.FindAsync(new object[] { volunteerId }, cancellationToken);
        if (volunteer == null || volunteer.VolunteerStatus != VolunteerStatus.Approved)
            throw new UnauthorizedAccessException("Only approved volunteers can start tours.");

        var request = await _context.TourRequests.FindAsync(new object[] { id }, cancellationToken);
        if (request == null) return false;

        if (request.RequesterId == volunteerId)
            throw new InvalidOperationException("Volunteers cannot start their own tour requests.");

        if (request.AssignedVolunteerId != volunteerId)
            throw new UnauthorizedAccessException("Only the assigned volunteer can start this tour.");

        var isBlocked = await _context.UserBlocks.AnyAsync(b =>
            (b.BlockerId == volunteerId && b.BlockedId == request.RequesterId) ||
            (b.BlockerId == request.RequesterId && b.BlockedId == volunteerId), cancellationToken);

        if (isBlocked)
            throw new InvalidOperationException("Cannot start a tour with a blocked user.");

        // Check if an active session already exists (idempotency)
        var existingActiveSession = await _context.TourSessions
            .FirstOrDefaultAsync(s => s.TourRequestId == id && s.Status == SessionStatus.Active, cancellationToken);
        if (existingActiveSession != null)
        {
            return true;
        }

        // Enforce state machine rules: Scheduled tours must be Confirmed; Instant tours must be Accepted or Confirmed
        if (request.Mode == SchedulingMode.Scheduled && request.Status != TourRequestStatus.Confirmed)
        {
            throw new InvalidOperationException("Scheduled tour requests must have a confirmed schedule before starting.");
        }

        if (request.Mode == SchedulingMode.Instant && request.Status != TourRequestStatus.Accepted && request.Status != TourRequestStatus.Confirmed)
        {
            throw new InvalidOperationException($"Cannot start tour request in status {request.Status}.");
        }

        // Atomically transition state and create session inside a database transaction
        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        request.TransitionTo(TourRequestStatus.Active);

        var session = new TourSession
        {
            Id = Guid.NewGuid().ToString(),
            TourRequestId = request.Id,
            HostVolunteerId = volunteerId,
            Status = SessionStatus.Active,
            StartedAt = DateTime.UtcNow
        };

        session.Participants.Add(new TourParticipant
        {
            TourSessionId = session.Id,
            UserId = volunteerId,
            Role = ParticipantRole.Host,
            JoinedAt = DateTime.UtcNow
        });

        session.Participants.Add(new TourParticipant
        {
            TourSessionId = session.Id,
            UserId = request.RequesterId,
            Role = ParticipantRole.Viewer,
            JoinedAt = DateTime.UtcNow
        });

        _context.TourSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await _notificationService.TourStartingAsync(session.Id, volunteerId, request.RequesterId);

        return true;
    }

    public async Task<bool> CompleteTourAsync(string id, string userId, CancellationToken cancellationToken = default)
    {
        var request = await _context.TourRequests.FindAsync(new object[] { id }, cancellationToken);
        if (request == null) return false;

        if (request.AssignedVolunteerId != userId && request.RequesterId != userId)
            throw new UnauthorizedAccessException("Only the requester or assigned volunteer can complete this tour.");

        request.TransitionTo(TourRequestStatus.Completed);

        // Complete any open active session
        var session = await _context.TourSessions
            .Where(s => s.TourRequestId == id && (s.Status == SessionStatus.Active || s.Status == SessionStatus.Starting))
            .FirstOrDefaultAsync(cancellationToken);

        if (session != null)
        {
            session.Status = SessionStatus.Completed;
            session.EndedAt = DateTime.UtcNow;
            session.EndReason = request.RequesterId == userId ? "Completed by requester" : "Completed by volunteer";
        }
        
        await _context.SaveChangesAsync(cancellationToken);

        var counterpartId = request.RequesterId == userId ? request.AssignedVolunteerId : request.RequesterId;
        if (!string.IsNullOrEmpty(counterpartId))
        {
            await _notificationService.TourEndedAsync(session?.Id ?? id, userId, counterpartId);
        }

        return true;
    }

    public async Task<IEnumerable<TourRequest>> GetNearbyRequestsAsync(string volunteerId, double radiusMeters, CancellationToken cancellationToken = default)
    {
        if (radiusMeters <= 0 || radiusMeters > 50000)
            throw new ArgumentException("Radius must be between 1 and 50,000 meters.");

        var volunteer = await _context.Users
            .Include(u => u.VolunteerSettings)
            .FirstOrDefaultAsync(u => u.Id == volunteerId, cancellationToken);

        if (volunteer == null || volunteer.VolunteerStatus != VolunteerStatus.Approved)
            throw new UnauthorizedAccessException("Only approved volunteers can view nearby tour requests.");

        // Enforce volunteer settings (M2)
        if (volunteer.VolunteerSettings != null)
        {
            if (!volunteer.VolunteerSettings.Enabled || !volunteer.VolunteerSettings.AvailableForRequests)
            {
                return Array.Empty<TourRequest>();
            }
        }

        var userLocation = await _context.UserLocations
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.UserId == volunteerId, cancellationToken);

        // Discard stale volunteer location (> 24 hours) (M16)
        if (userLocation == null || userLocation.LastUpdatedAt < DateTime.UtcNow.AddHours(-24))
        {
            return Array.Empty<TourRequest>();
        }

        // Exclude blocked user requests
        var blockedUserIds = await _context.UserBlocks
            .Where(b => b.BlockerId == volunteerId || b.BlockedId == volunteerId)
            .Select(b => b.BlockerId == volunteerId ? b.BlockedId : b.BlockerId)
            .ToListAsync(cancellationToken);

        // Determine accepted visibility modes based on volunteer settings (M1, M2)
        bool acceptsPrivate = volunteer.VolunteerSettings == null || volunteer.VolunteerSettings.AcceptsPrivateTours;
        bool acceptsGroup = volunteer.VolunteerSettings == null || volunteer.VolunteerSettings.AcceptsGroupTours;

        if (!acceptsPrivate && !acceptsGroup)
        {
            return Array.Empty<TourRequest>();
        }

        // 1 degree latitude ~= 111,320 meters
        double latDegreeDelta = radiusMeters / 111320.0;
        double cosineLat = Math.Cos(userLocation.Location.Y * Math.PI / 180.0);
        double lngDegreeDelta = radiusMeters / (111320.0 * Math.Max(0.01, Math.Abs(cosineLat)));
        double maxDegreeDistance = Math.Max(latDegreeDelta, lngDegreeDelta);

        // Spatial query using GiST index (ST_DWithin via IsWithinDistance) and pagination (M15)
        var candidates = await _context.TourRequests
            .Include(r => r.Place)
            .Where(r => r.Status == TourRequestStatus.Published)
            .Where(r => !blockedUserIds.Contains(r.RequesterId))
            .Where(r => (acceptsPrivate && r.Visibility == VisibilityMode.Private) ||
                        (acceptsGroup && r.Visibility == VisibilityMode.Group))
            .Where(r => r.Place != null && r.Place.Location.IsWithinDistance(userLocation.Location, maxDegreeDistance))
            .AsNoTracking()
            .Take(50)
            .ToListAsync(cancellationToken);

        // Accurate Haversine metric distance check
        var nearbyRequests = candidates.Where(r =>
        {
            if (r.Place == null) return false;
            double distance = CalculateDistanceMeters(
                userLocation.Location.Y, userLocation.Location.X,
                r.Place.Location.Y, r.Place.Location.X);
            return distance <= radiusMeters;
        }).ToList();

        return nearbyRequests;
    }

    private static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371000; // Earth radius in meters
        double dLat = (lat2 - lat1) * Math.PI / 180.0;
        double dLon = (lon2 - lon1) * Math.PI / 180.0;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return r * c;
    }
}
