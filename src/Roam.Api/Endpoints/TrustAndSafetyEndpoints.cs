using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Roam.Application.TrustAndSafety;

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
    }
}
