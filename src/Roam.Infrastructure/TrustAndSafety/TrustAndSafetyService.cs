using Microsoft.EntityFrameworkCore;
using Roam.Application.TrustAndSafety;
using Roam.Domain.TrustAndSafety;
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
}
