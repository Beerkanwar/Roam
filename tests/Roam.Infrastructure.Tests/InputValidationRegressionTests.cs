using Roam.Application.TrustAndSafety;
using Roam.Domain.TrustAndSafety;
using Roam.Infrastructure.Common;
using Roam.Infrastructure.Places;
using Roam.Infrastructure.TourRequests;
using Roam.Infrastructure.TrustAndSafety;
using Xunit;

namespace Roam.Infrastructure.Tests;

/// <summary>
/// Regression tests for input validation that previously surfaced as database errors (HTTP 500) or
/// silently accepted out-of-range values. Services are built without a DbContext, which also proves the
/// validation runs before any database access.
/// </summary>
public class InputValidationRegressionTests
{
    private static readonly string TooLong = new('x', 2001);

    [Fact]
    public async Task SubmitReport_RejectsUndefinedReason()
    {
        var service = new TrustAndSafetyService(null!);
        var dto = new CreateReportDto { ReporterId = "a", ReportedUserId = "b", Reason = (ReportReason)999 };

        await Assert.ThrowsAsync<ArgumentException>(() => service.SubmitReportAsync(dto));
    }

    [Fact]
    public async Task SubmitReport_RejectsUndefinedSeverity()
    {
        var service = new TrustAndSafetyService(null!);
        var dto = new CreateReportDto { ReporterId = "a", ReportedUserId = "b", Severity = (ReportSeverity)(-1) };

        await Assert.ThrowsAsync<ArgumentException>(() => service.SubmitReportAsync(dto));
    }

    [Fact]
    public async Task SubmitReport_RejectsDescriptionLongerThanColumn()
    {
        var service = new TrustAndSafetyService(null!);
        var dto = new CreateReportDto { ReporterId = "a", ReportedUserId = "b", Description = TooLong };

        await Assert.ThrowsAsync<ArgumentException>(() => service.SubmitReportAsync(dto));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SubmitReport_RejectsMissingReportedUser(string reportedUserId)
    {
        var service = new TrustAndSafetyService(null!);
        var dto = new CreateReportDto { ReporterId = "a", ReportedUserId = reportedUserId };

        await Assert.ThrowsAsync<ArgumentException>(() => service.SubmitReportAsync(dto));
    }

    [Fact]
    public async Task BlockUser_RejectsBlankTarget()
    {
        var service = new TrustAndSafetyService(null!);

        await Assert.ThrowsAsync<ArgumentException>(() => service.BlockUserAsync("a", " "));
    }

    [Fact]
    public async Task ResolveReport_RejectsUndefinedAction()
    {
        var service = new TrustAndSafetyService(null!);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ResolveReportAsync("r", (ModerationAction)42, "mod", "reason"));
    }

    [Fact]
    public async Task ResolveReport_RejectsReasonLongerThanAuditColumn()
    {
        var service = new TrustAndSafetyService(null!);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ResolveReportAsync("r", ModerationAction.Warning, "mod", TooLong));
    }

    [Fact]
    public async Task SubmitFeedback_RejectsUndefinedRating()
    {
        var service = new FeedbackService(null!);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SubmitFeedbackAsync("session", "reviewer", "target", (FeedbackRating)0, null));
    }

    [Fact]
    public async Task SubmitFeedback_RejectsCommentsLongerThanColumn()
    {
        var service = new FeedbackService(null!);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SubmitFeedbackAsync("session", "reviewer", "target", FeedbackRating.Positive, TooLong));
    }

    [Fact]
    public async Task CancelRequest_RejectsOversizedReason()
    {
        var service = new TourRequestService(null!, null!);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CancelRequestAsync("req", "user", new string('x', TourRequestService.MaxCancellationReasonLength + 1)));
    }

    [Fact]
    public async Task ProposeSchedule_InterpretsLocalTimesAsTheInstantTheyRepresent()
    {
        // A Local-kind value 2 minutes from now is inside the 5-minute minimum lead time. The old code
        // relabelled the local wall-clock time as UTC, which on any UTC+ server pushed it hours into the
        // future and accepted it.
        var service = new TourRequestService(null!, null!);
        var localStart = DateTime.Now.AddMinutes(2);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ProposeScheduleAsync("req", "user", localStart));
    }

    [Fact]
    public void UtcDateTime_Normalize_HandlesAllKinds()
    {
        var utc = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(utc, UtcDateTime.Normalize(utc));
        Assert.Equal(DateTimeKind.Utc, UtcDateTime.Normalize(utc.ToLocalTime()).Kind);
        Assert.Equal(utc, UtcDateTime.Normalize(utc.ToLocalTime()));

        var unspecified = DateTime.SpecifyKind(utc, DateTimeKind.Unspecified);
        var normalized = UtcDateTime.Normalize(unspecified);
        Assert.Equal(DateTimeKind.Utc, normalized.Kind);
        Assert.Equal(utc, normalized);
    }

    [Theory]
    [InlineData("100%", "100\\%")]
    [InlineData("a_b", "a\\_b")]
    [InlineData("c:\\path", "c:\\\\path")]
    [InlineData("plain", "plain")]
    public void EscapeLikePattern_EscapesWildcards(string input, string expected)
    {
        Assert.Equal(expected, PlaceService.EscapeLikePattern(input));
    }
}
