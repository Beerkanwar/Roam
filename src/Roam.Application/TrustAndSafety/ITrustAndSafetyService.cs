using Roam.Domain.TrustAndSafety;

namespace Roam.Application.TrustAndSafety;

public class CreateReportDto
{
    public string ReporterId { get; set; } = string.Empty;
    public string ReportedUserId { get; set; } = string.Empty;
    public string TourSessionId { get; set; } = string.Empty;
    public ReportReason Reason { get; set; }
    public string Description { get; set; } = string.Empty;
    public ReportSeverity Severity { get; set; } = ReportSeverity.Low;
}

public interface ITrustAndSafetyService
{
    Task<Report> SubmitReportAsync(CreateReportDto dto, CancellationToken cancellationToken = default);
    Task AppendReliabilityEventAsync(string userId, ReliabilityEventType type, string sessionId, int weight, CancellationToken cancellationToken = default);
    Task<string> GetUserReliabilitySummaryAsync(string userId, CancellationToken cancellationToken = default);
}
