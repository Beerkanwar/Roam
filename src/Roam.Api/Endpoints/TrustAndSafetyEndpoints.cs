using Microsoft.AspNetCore.Mvc;
using Roam.Application.TrustAndSafety;

namespace Roam.Api.Endpoints;

public static class TrustAndSafetyEndpoints
{
    public static void MapTrustAndSafetyEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/api/v1/trust-safety")
            .WithTags("TrustAndSafety")
            .WithApiVersionSet(builder.NewApiVersionSet().Build());

        group.MapPost("/reports", async ([FromBody] CreateReportDto dto, ITrustAndSafetyService service, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrEmpty(dto.ReporterId))
                dto.ReporterId = "user_" + Guid.NewGuid().ToString("N")[..8]; // MVP fallback

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
