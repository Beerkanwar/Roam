namespace Roam.Domain.TrustAndSafety;

public class Report
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ReporterId { get; set; } = string.Empty;
    public string ReportedUserId { get; set; } = string.Empty;
    public string TourSessionId { get; set; } = string.Empty;
    
    public ReportReason Reason { get; set; }
    public string Description { get; set; } = string.Empty;
    public ReportSeverity Severity { get; set; } = ReportSeverity.Low;
    public ReportStatus Status { get; set; } = ReportStatus.Pending;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}
