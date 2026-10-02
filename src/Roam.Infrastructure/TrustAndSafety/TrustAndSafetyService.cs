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
        
        // As a side-effect, we can log a reliability event against the reported user for tracking
        var reliabilityEvent = new ReliabilityEvent
        {
            UserId = dto.ReportedUserId,
            Type = ReliabilityEventType.ValidatedReport, // Assuming valid for MVP
            RelatedSessionId = dto.TourSessionId,
            Weight = -10 // Penalty
        };
        _context.ReliabilityEvents.Add(reliabilityEvent);

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
            Weight = weight
        };

        _context.ReliabilityEvents.Add(ev);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> GetUserReliabilitySummaryAsync(string userId, CancellationToken cancellationToken = default)
    {
        var events = await _context.ReliabilityEvents
            .Where(e => e.UserId == userId)
            .ToListAsync(cancellationToken);

        int completedTours = events.Count(e => e.Type == ReliabilityEventType.CompletedTour);
        int totalScore = events.Sum(e => e.Weight);

        // Map internal score to a public-facing description
        string feedbackLevel = totalScore >= 0 ? "Positive" : "Needs Improvement";

        return $"{completedTours} tours completed. Community feedback: {feedbackLevel}.";
    }

    public async Task BlockUserAsync(string blockerId, string blockedId, CancellationToken cancellationToken = default)
    {
        var exists = await _context.UserBlocks
            .AnyAsync(ub => ub.BlockerId == blockerId && ub.BlockedId == blockedId, cancellationToken);
            
        if (!exists)
        {
            _context.UserBlocks.Add(new UserBlock
            {
                BlockerId = blockerId,
                BlockedId = blockedId
            });
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
        
        report.Status = action == ModerationAction.NoAction ? ReportStatus.Dismissed : ReportStatus.ActionTaken;
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
        
        // Apply user status changes
        var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == modCase.TargetUserId, cancellationToken);
        if (targetUser != null)
        {
            if (action == ModerationAction.Suspension)
            {
                targetUser.AccountStatus = AccountStatus.Suspended;
            }
            else if (action == ModerationAction.PermanentRemoval)
            {
                targetUser.AccountStatus = AccountStatus.Banned;
            }
        }
        
        await _context.SaveChangesAsync(cancellationToken);
    }
}
