using Microsoft.EntityFrameworkCore;
using Roam.Application.TrustAndSafety;
using Roam.Domain.TrustAndSafety;
using Roam.Domain.Users;
using Roam.Infrastructure.Persistence;

namespace Roam.Infrastructure.TrustAndSafety;

public class TrustAndSafetyService : ITrustAndSafetyService
{
    private readonly ApplicationDbContext _context;

    public TrustAndSafetyService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Report> SubmitReportAsync(CreateReportDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.ReporterId == dto.ReportedUserId)
            throw new InvalidOperationException("Users cannot submit reports against themselves.");

        var reportedUserExists = await _context.Users.AnyAsync(u => u.Id == dto.ReportedUserId, cancellationToken);
        if (!reportedUserExists)
            throw new ArgumentException("The reported user does not exist.", nameof(dto.ReportedUserId));

        // Verify session participation if a session ID was provided
        if (!string.IsNullOrEmpty(dto.TourSessionId))
        {
            var session = await _context.TourSessions
                .Include(s => s.Participants)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == dto.TourSessionId, cancellationToken);

            if (session == null)
                throw new ArgumentException("Specified tour session not found.", nameof(dto.TourSessionId));

            bool reporterInSession = session.HostVolunteerId == dto.ReporterId || session.Participants.Any(p => p.UserId == dto.ReporterId);
            bool reportedInSession = session.HostVolunteerId == dto.ReportedUserId || session.Participants.Any(p => p.UserId == dto.ReportedUserId);

            if (!reporterInSession || !reportedInSession)
                throw new InvalidOperationException("Both the reporter and reported user must have participated in the specified session.");
        }

        // Deduplicate: check if an identical pending report exists
        bool alreadyPending = await _context.Reports.AnyAsync(r =>
            r.ReporterId == dto.ReporterId &&
            r.ReportedUserId == dto.ReportedUserId &&
            r.TourSessionId == dto.TourSessionId &&
            r.Status == ReportStatus.Pending, cancellationToken);

        if (alreadyPending)
        {
            throw new InvalidOperationException("A report against this user for this session is already pending review.");
        }

        var report = new Report
        {
            ReporterId = dto.ReporterId,
            ReportedUserId = dto.ReportedUserId,
            TourSessionId = dto.TourSessionId,
            Reason = dto.Reason,
            Description = dto.Description,
            Severity = dto.Severity,
            Status = ReportStatus.Pending
        };

        _context.Reports.Add(report);
        
        var moderationCase = new ModerationCase
        {
            ReportId = report.Id,
            TargetUserId = dto.ReportedUserId
        };
        _context.ModerationCases.Add(moderationCase);

        // Note: Reliability penalties are not applied on report submission.
        // They are only applied after a confirmed moderation action in ResolveReportAsync.

        await _context.SaveChangesAsync(cancellationToken);

        return report;
    }

    public async Task AppendReliabilityEventAsync(string userId, ReliabilityEventType type, string sessionId, int weight, CancellationToken cancellationToken = default)
    {
        var ev = new ReliabilityEvent
        {
            UserId = userId,
            Type = type,
            RelatedSessionId = sessionId,
            Weight = weight,
            CreatedAt = DateTime.UtcNow
        };
        _context.ReliabilityEvents.Add(ev);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> GetUserReliabilitySummaryAsync(string userId, CancellationToken cancellationToken = default)
    {
        var events = await _context.ReliabilityEvents
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        var score = events.Sum(e => e.Weight);

        return score switch
        {
            >= 50 => "Trusted",
            >= 10 => "Normal",
            _ => "Needs Improvement"
        };
    }

    public async Task BlockUserAsync(string blockerId, string blockedId, CancellationToken cancellationToken = default)
    {
        if (blockerId == blockedId) return;

        var exists = await _context.UserBlocks
            .AnyAsync(ub => ub.BlockerId == blockerId && ub.BlockedId == blockedId, cancellationToken);

        if (!exists)
        {
            _context.UserBlocks.Add(new UserBlock(blockerId, blockedId));
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task UnblockUserAsync(string blockerId, string blockedId, CancellationToken cancellationToken = default)
    {
        var block = await _context.UserBlocks
            .FirstOrDefaultAsync(ub => ub.BlockerId == blockerId && ub.BlockedId == blockedId, cancellationToken);

        if (block != null)
        {
            _context.UserBlocks.Remove(block);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IEnumerable<string>> GetBlockedUsersAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.UserBlocks
            .Where(ub => ub.BlockerId == userId)
            .Select(ub => ub.BlockedId)
            .ToListAsync(cancellationToken);
    }

    public async Task ResolveReportAsync(string reportId, ModerationAction action, string moderatorId, string reason, CancellationToken cancellationToken = default)
    {
        var report = await _context.Reports.FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken);
        if (report == null) throw new ArgumentException("Report not found.");
        
        var modCase = await _context.ModerationCases.FirstOrDefaultAsync(m => m.ReportId == reportId, cancellationToken);
        if (modCase == null) throw new ArgumentException("Moderation case not found.");

        // Idempotency check: if report or case is already resolved/closed, do not re-process
        if (report.Status != ReportStatus.Pending || modCase.Status == ModerationCaseStatus.Closed)
        {
            return;
        }
        
        report.Status = action == ModerationAction.NoAction ? ReportStatus.Dismissed : ReportStatus.ActionTaken;
        report.ResolvedAt = DateTime.UtcNow;
        modCase.Status = ModerationCaseStatus.Closed;
        modCase.ClosedAt = DateTime.UtcNow;
        
        var auditLog = new ModerationAuditLog
        {
            ModerationCaseId = modCase.Id,
            TargetUserId = modCase.TargetUserId,
            ModeratorId = moderatorId,
            Action = action,
            Reason = reason
        };
        _context.ModerationAuditLogs.Add(auditLog);
        
        // Apply user status changes and rotate security stamp
        var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == modCase.TargetUserId, cancellationToken);
        if (targetUser != null)
        {
            if (action == ModerationAction.Suspension)
            {
                targetUser.AccountStatus = AccountStatus.Suspended;
                targetUser.SecurityStamp = Guid.NewGuid().ToString();
            }
            else if (action == ModerationAction.PermanentRemoval)
            {
                targetUser.AccountStatus = AccountStatus.Banned;
                targetUser.SecurityStamp = Guid.NewGuid().ToString();
            }
        }

        // Apply reliability penalties only upon verified adverse action
        if (action != ModerationAction.NoAction)
        {
            int penaltyWeight = action switch
            {
                ModerationAction.Warning => -5,
                ModerationAction.Suspension => -15,
                ModerationAction.PermanentRemoval => -30,
                _ => -10
            };

            var reliabilityEvent = new ReliabilityEvent
            {
                UserId = modCase.TargetUserId,
                Type = ReliabilityEventType.ValidatedReport,
                RelatedSessionId = report.TourSessionId,
                Weight = penaltyWeight,
                CreatedAt = DateTime.UtcNow
            };
            _context.ReliabilityEvents.Add(reliabilityEvent);
        }
        
        await _context.SaveChangesAsync(cancellationToken);
    }
}
