namespace Roam.Domain.TrustAndSafety;

public enum ReliabilityEventType
{
    CompletedTour,
    CancelledEarly,
    LateCancellation,
    NoShow,
    ConfirmedAttendance,
    ValidatedReport,
    ModerationViolation
}
