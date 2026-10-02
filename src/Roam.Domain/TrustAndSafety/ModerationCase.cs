using System;

namespace Roam.Domain.TrustAndSafety;

public enum ModerationCaseStatus
{
    Open = 0,
    Closed = 1
}

public class ModerationCase
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? ReportId { get; set; } // Nullable if created manually, not from a report
    public string TargetUserId { get; set; } = string.Empty;
    public ModerationCaseStatus Status { get; set; } = ModerationCaseStatus.Open;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
}
