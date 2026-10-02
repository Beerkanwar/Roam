using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Roam.Application.TrustAndSafety;
using Roam.Domain.TrustAndSafety;

namespace Roam.Api.Endpoints;

public static class TrustAndSafetyEndpoints
{
    public static void MapTrustAndSafetyEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/api/v1/trust-safety")
            .WithTags("TrustAndSafety")
            .WithApiVersionSet(builder.NewApiVersionSet().Build())
            .RequireAuthorization();

        group.MapPost("/reports", async (ClaimsPrincipal user, [FromBody] CreateReportDto dto, ITrustAndSafetyService service, CancellationToken cancellationToken) =>
        {
            dto.ReporterId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User ID missing from token.");

            var report = await service.SubmitReportAsync(dto, cancellationToken);
            return Results.Created($"/api/v1/trust-safety/reports/{report.Id}", report);
        });

        group.MapGet("/users/{userId}/reliability", async (string userId, ITrustAndSafetyService service, CancellationToken cancellationToken) =>
        {
            var summary = await service.GetUserReliabilitySummaryAsync(userId, cancellationToken);
            return Results.Ok(new { summary });
        });

        // Blocking endpoints
        group.MapPost("/blocks/{blockedId}", async (string blockedId, ClaimsPrincipal user, ITrustAndSafetyService service, CancellationToken cancellationToken) =>
        {
            var blockerId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User ID missing.");
            await service.BlockUserAsync(blockerId, blockedId, cancellationToken);
            return Results.Ok();
        });

        group.MapDelete("/blocks/{blockedId}", async (string blockedId, ClaimsPrincipal user, ITrustAndSafetyService service, CancellationToken cancellationToken) =>
        {
            var blockerId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User ID missing.");
            await service.UnblockUserAsync(blockerId, blockedId, cancellationToken);
            return Results.NoContent();
        });

        group.MapGet("/blocks", async (ClaimsPrincipal user, ITrustAndSafetyService service, CancellationToken cancellationToken) =>
        {
            var blockerId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User ID missing.");
            var blocks = await service.GetBlockedUsersAsync(blockerId, cancellationToken);
            return Results.Ok(blocks);
        });

        // Moderation endpoints (Admin)
        group.MapPost("/moderation/resolve/{reportId}", async (string reportId, [FromBody] ResolveReportRequest request, ClaimsPrincipal user, ITrustAndSafetyService service, CancellationToken cancellationToken) =>
        {
            var moderatorId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User ID missing.");
            // Note: Should have RequireAuthorization("RequireAdminRole") here in a real production environment.
            
            try
            {
                await service.ResolveReportAsync(reportId, request.Action, moderatorId, request.Reason, cancellationToken);
                return Results.Ok();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });
        group.MapPost("/feedback", async ([FromBody] SubmitFeedbackRequest request, [FromServices] IFeedbackService feedbackService, ClaimsPrincipal user) => 
        {
            var reviewerIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(reviewerIdStr, out var reviewerId)) return Results.Unauthorized();

            var feedback = await feedbackService.SubmitFeedbackAsync(request.TourSessionId, reviewerId, request.TargetUserId, request.Rating, request.Comments);
            return Results.Ok(new { feedback.Id, feedback.Rating });
        })
        .WithName("SubmitFeedback")
        .WithSummary("Submit post-session feedback");
    }
}

public class SubmitFeedbackRequest
{
    public Guid TourSessionId { get; set; }
    public Guid TargetUserId { get; set; }
    public FeedbackRating Rating { get; set; }
    public string? Comments { get; set; }
}

public class ResolveReportRequest
{
    public ModerationAction Action { get; set; }
    public string Reason { get; set; } = string.Empty;
}
