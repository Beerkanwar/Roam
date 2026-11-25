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
using Roam.Infrastructure.Common;
using Roam.Infrastructure.Persistence;

namespace Roam.Infrastructure.TourRequests;

public class TourRequestService : ITourRequestService
{
    internal const int MaxCancellationReasonLength = 500;

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
        var requester = await _context.Users.FindAsync(new object[] { dto.RequesterId }, cancellationToken);
        if (requester == null || requester.AgeCategory != Roam.Domain.Users.AgeCategory.Adult)
        {
            throw new UnauthorizedAccessException("Only age-verified adult users (18+) can create tour requests.");
        }

        ValidateModes(dto.Visibility, dto.Mode);

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
        var requester = await _context.Users.FindAsync(new object[] { dto.RequesterId }, cancellationToken);
        if (requester == null || requester.AgeCategory != Roam.Domain.Users.AgeCategory.Adult)
        {
            throw new UnauthorizedAccessException("Only age-verified adult users (18+) can create tour requests.");
        }

        ValidateModes(dto.Visibility, dto.Mode);

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
        var startUtc = UtcDateTime.Normalize(startTime);
        DateTime? endUtc = endTime.HasValue ? UtcDateTime.Normalize(endTime.Value) : null;

        if (startUtc < DateTime.UtcNow.AddMinutes(5))
            throw new ArgumentException("Proposed start time must be at least 5 minutes in the future.");

        if (endUtc.HasValue && endUtc.Value <= startUtc)
            throw new ArgumentException("Proposed end time must be after the start time.");

        var request = await _context.TourRequests.FindAsync(new object[] { requestId }, cancellationToken);
        if (request == null) throw new InvalidOperationException("Request not found");

        if (request.RequesterId != userId && request.AssignedVolunteerId != userId)
            throw new UnauthorizedAccessException("User is not authorized to propose a schedule for this request.");

        // Schedules can only be negotiated before the tour is confirmed; proposing on a cancelled,
        // expired, active or completed request would create proposals that can never be honoured.
        if (request.Status is not (TourRequestStatus.Published or TourRequestStatus.Matching or
                                   TourRequestStatus.VolunteerInterested or TourRequestStatus.Accepted))
            throw new InvalidOperationException($"Cannot propose a schedule for a tour request in status {request.Status}.");

        var schedule = new ScheduledTour
        {
            TourRequestId = requestId,
            ProposedByUserId = userId,
            ProposedStartUtc = startUtc,
            ProposedEndUtc = endUtc
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

        if (schedule.ProposedStartUtc <= DateTime.UtcNow)
            throw new InvalidOperationException("Cannot accept a schedule whose proposed start time has already passed.");

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
        if (reason?.Length > MaxCancellationReasonLength)
            throw new ArgumentException($"Cancellation reason cannot exceed {MaxCancellationReasonLength} characters.", nameof(reason));

        var request = await _context.TourRequests.FindAsync(new object[] { id }, cancellationToken);
        if (request == null) return false;

        if (request.RequesterId != userId && request.AssignedVolunteerId != userId)
            throw new UnauthorizedAccessException("Only the requester or assigned volunteer can cancel this tour.");

        // The assigned volunteer stepping away is a withdrawal, not a cancellation: the requester still
        // wants the tour, so the request goes back to discovery for other volunteers.
        if (request.AssignedVolunteerId == userId && request.RequesterId != userId)
        {
            if (!request.CanReleaseVolunteer)
                throw new InvalidOperationException($"Cannot withdraw from a tour request in status {request.Status}.");

            request.ReleaseVolunteer();
            await CancelOpenSchedulesAsync(new[] { request.Id }, userId, "Volunteer withdrew", cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            await _notificationService.VolunteerWithdrawnAsync(request.Id, request.RequesterId);
            await _notificationService.TourRequestCreatedAsync(request.Id);
            return true;
        }

        request.TransitionTo(TourRequestStatus.Cancelled);

        request.CancelledByUserId = userId;
        request.CancellationReason = reason?.Trim() ?? string.Empty;
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

    /// <summary>
    /// Detaches a user who is deleting their account from tours where they are the volunteer:
    /// pre-start assignments return to discovery and live sessions they are part of are ended.
    /// Changes are saved on the shared DbContext (so they join the caller's transaction); the returned
    /// notifications should be sent by the caller after it commits.
    /// </summary>
    public async Task<AccountDeletionTourChanges> ReleaseToursForDeletedUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var changes = new AccountDeletionTourChanges();
        var now = DateTime.UtcNow;

        var assigned = await _context.TourRequests
            .Where(r => r.AssignedVolunteerId == userId && r.RequesterId != userId &&
                        (r.Status == TourRequestStatus.VolunteerInterested ||
                         r.Status == TourRequestStatus.Accepted ||
                         r.Status == TourRequestStatus.Confirmed))
            .ToListAsync(cancellationToken);

        foreach (var request in assigned)
        {
            request.ReleaseVolunteer();
            changes.RepublishedRequests.Add((request.Id, request.RequesterId));
        }

        await CancelOpenSchedulesAsync(assigned.Select(r => r.Id).ToList(), userId, "Volunteer account deleted", cancellationToken);

        // Live sessions the user hosts or participates in cannot continue without them.
        var sessions = await _context.TourSessions
            .Include(s => s.Participants)
            .Where(s => (s.Status == SessionStatus.Active || s.Status == SessionStatus.Starting) &&
                        (s.HostVolunteerId == userId || s.Participants.Any(p => p.UserId == userId)))
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.Status = SessionStatus.Ended;
            session.EndedAt = now;
            session.EndReason = "Participant account deleted";

            var request = await _context.TourRequests.FindAsync(new object[] { session.TourRequestId }, cancellationToken);
            if (request != null && request.CanTransitionTo(TourRequestStatus.Completed))
                request.TransitionTo(TourRequestStatus.Completed);

            var others = session.Participants.Select(p => p.UserId)
                .Append(session.HostVolunteerId)
                .Where(id => id != userId)
                .Distinct();
            foreach (var other in others)
                changes.EndedSessions.Add((session.Id, other));
        }

        await _context.SaveChangesAsync(cancellationToken);
        return changes;
    }

    public async Task<IReadOnlyList<MyTourDto>> GetMyToursAsync(string userId, CancellationToken cancellationToken = default)
    {
        var openStatuses = new[]
        {
            TourRequestStatus.Published, TourRequestStatus.Matching, TourRequestStatus.VolunteerInterested,
            TourRequestStatus.Accepted, TourRequestStatus.Confirmed, TourRequestStatus.Active
        };

        var requests = await _context.TourRequests
            .AsNoTracking()
            .Include(r => r.Place)
            .Where(r => (r.RequesterId == userId || r.AssignedVolunteerId == userId) && openStatuses.Contains(r.Status))
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        var requestIds = requests.Select(r => r.Id).ToList();
        var activeSessions = await _context.TourSessions
            .AsNoTracking()
            .Where(s => requestIds.Contains(s.TourRequestId) &&
                        (s.Status == SessionStatus.Active || s.Status == SessionStatus.Starting))
            .Select(s => new { s.TourRequestId, s.Id })
            .ToListAsync(cancellationToken);

        // The other person in each tour, so clients can show who they are touring with and report them.
        var counterpartIds = requests
            .Select(r => r.RequesterId == userId ? r.AssignedVolunteerId : r.RequesterId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();
        var counterpartNames = await _context.Users
            .AsNoTracking()
            .Where(u => counterpartIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        return requests.Select(r => new MyTourDto
        {
            Id = r.Id,
            Role = r.RequesterId == userId ? "Requester" : "Volunteer",
            Status = (int)r.Status,
            StatusName = r.Status.ToString(),
            Mode = (int)r.Mode,
            Visibility = (int)r.Visibility,
            Description = r.Description,
            PlaceName = r.Place?.Name,
            Latitude = r.Place?.Location.Y,
            Longitude = r.Place?.Location.X,
            CreatedAt = r.CreatedAt,
            ExpiresAt = r.ExpiresAt,
            HasVolunteer = !string.IsNullOrEmpty(r.AssignedVolunteerId),
            ActiveSessionId = activeSessions.FirstOrDefault(s => s.TourRequestId == r.Id)?.Id,
            CounterpartId = r.RequesterId == userId ? r.AssignedVolunteerId : r.RequesterId,
            CounterpartName = (r.RequesterId == userId ? r.AssignedVolunteerId : r.RequesterId) is { } otherId
                && counterpartNames.TryGetValue(otherId, out var otherName) ? otherName : null
        }).ToList();
    }

    private async Task CancelOpenSchedulesAsync(IReadOnlyCollection<string> requestIds, string cancelledByUserId, string reason, CancellationToken cancellationToken)
    {
        if (requestIds.Count == 0) return;

        var openSchedules = await _context.ScheduledTours
            .Where(s => requestIds.Contains(s.TourRequestId) &&
                        (s.Status == ScheduledTourStatus.Proposed || s.Status == ScheduledTourStatus.Accepted))
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var schedule in openSchedules)
        {
            schedule.Status = ScheduledTourStatus.Cancelled;
            schedule.CancelledByUserId = cancelledByUserId;
            schedule.CancellationReason = reason;
            schedule.CancelledAt = now;
            schedule.UpdatedAt = now;
        }
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

        // Validates the state machine (throws InvalidOperationException on an invalid transition).
        if (!request.CanTransitionTo(TourRequestStatus.VolunteerInterested))
            throw new InvalidOperationException($"Cannot transition from {request.Status} to {TourRequestStatus.VolunteerInterested}");

        // Atomic conditional update: two volunteers expressing interest concurrently must not both
        // "win" (a tracked read-modify-write would let the last writer silently overwrite the first).
        var rowsAffected = await _context.TourRequests
            .Where(r => r.Id == id &&
                        (r.Status == TourRequestStatus.Published || r.Status == TourRequestStatus.Matching) &&
                        (r.AssignedVolunteerId == null || r.AssignedVolunteerId == volunteerId))
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, TourRequestStatus.VolunteerInterested)
                .SetProperty(r => r.AssignedVolunteerId, volunteerId), cancellationToken);

        if (rowsAffected == 0)
            throw new InvalidOperationException("Request has already been claimed by another volunteer.");

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

        // Validate the transition in-memory, then claim it with a conditional UPDATE so two concurrent
        // start calls cannot both create a session for the same request.
        if (!request.CanTransitionTo(TourRequestStatus.Active))
            throw new InvalidOperationException($"Cannot transition from {request.Status} to {TourRequestStatus.Active}");

        var expectedStatus = request.Status;
        var claimed = await _context.TourRequests
            .Where(r => r.Id == id && r.Status == expectedStatus && r.AssignedVolunteerId == volunteerId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, TourRequestStatus.Active), cancellationToken);

        if (claimed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            // Another call started the tour first; treat as idempotent success if its session exists.
            var startedConcurrently = await _context.TourSessions
                .AnyAsync(s => s.TourRequestId == id && s.Status == SessionStatus.Active, cancellationToken);
            if (startedConcurrently) return true;
            throw new InvalidOperationException("Tour request state changed while starting the tour. Please retry.");
        }

        // Keep the tracked entity consistent with the row we just updated (no second UPDATE is issued
        // because the original value is reset to match).
        request.Status = TourRequestStatus.Active;
        _context.Entry(request).Property(r => r.Status).OriginalValue = TourRequestStatus.Active;

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

        var now = DateTime.UtcNow;

        // Spatial query using GiST index (ST_DWithin via IsWithinDistance) and pagination (M15)
        var candidates = await _context.TourRequests
            .Include(r => r.Place)
            .Where(r => r.Status == TourRequestStatus.Published)
            .Where(r => r.RequesterId != volunteerId)
            .Where(r => !r.ExpiresAt.HasValue || r.ExpiresAt > now)
            .Where(r => !blockedUserIds.Contains(r.RequesterId))
            .Where(r => (acceptsPrivate && r.Visibility == VisibilityMode.Private) ||
                        (acceptsGroup && r.Visibility == VisibilityMode.Group))
            .Where(r => r.Place != null && r.Place.Location.IsWithinDistance(userLocation.Location, maxDegreeDistance))
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt) // newest requests first
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

    private static void ValidateModes(VisibilityMode visibility, SchedulingMode mode)
    {
        if (!Enum.IsDefined(visibility))
            throw new ArgumentException("Invalid visibility mode.", nameof(visibility));
        if (!Enum.IsDefined(mode))
            throw new ArgumentException("Invalid scheduling mode.", nameof(mode));
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
