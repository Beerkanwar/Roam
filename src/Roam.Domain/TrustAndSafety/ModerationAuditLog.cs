using System;

namespace Roam.Domain.TrustAndSafety;

public class ModerationAuditLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ModerationCaseId { get; set; } = string.Empty;
    public string TargetUserId { get; set; } = string.Empty;
    public string ModeratorId { get; set; } = string.Empty;
    
    public ModerationAction Action { get; set; } = ModerationAction.NoAction;
    public string Reason { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
