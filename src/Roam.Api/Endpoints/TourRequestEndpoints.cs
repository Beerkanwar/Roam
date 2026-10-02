using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Roam.Application.TourRequests;

namespace Roam.Api.Endpoints;

public static class TourRequestEndpoints
{
    public static void MapTourRequestEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/api/v1/tour-requests")
            .WithTags("TourRequests")
            .WithApiVersionSet(builder.NewApiVersionSet().Build())
            .RequireAuthorization();

        group.MapPost("/", async (ClaimsPrincipal user, [FromBody] CreateTourRequestDto dto, ITourRequestService service, CancellationToken cancellationToken) =>
        {
            dto.RequesterId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User ID missing from token.");

            var request = await service.CreateRequestAsync(dto, cancellationToken);
            return Results.Created($"/api/v1/tour-requests/{request.Id}", request);
        });

        group.MapPost("/coordinates", async (ClaimsPrincipal user, [FromBody] CreateTourRequestByCoordinatesDto dto, ITourRequestService service, CancellationToken cancellationToken) =>
        {
            dto.RequesterId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User ID missing from token.");

            var request = await service.CreateRequestByCoordinatesAsync(dto, cancellationToken);
            return Results.Created($"/api/v1/tour-requests/{request.Id}", request);
        });

        group.MapPost("/{id}/accept", async (string id, ClaimsPrincipal user, ITourRequestService service, CancellationToken cancellationToken) =>
        {
            var volunteerId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User ID missing from token.");
            var success = await service.AcceptRequestAsync(id, volunteerId, cancellationToken);
            return success ? Results.Ok() : Results.BadRequest("Cannot accept request from current state.");
        });

        group.MapPost("/{id}/schedule", async (string id, [FromBody] DateTime scheduledTime, ITourRequestService service, CancellationToken cancellationToken) =>
        {
            var success = await service.ScheduleRequestAsync(id, scheduledTime, cancellationToken);
            return success ? Results.Ok() : Results.BadRequest("Cannot schedule request from current state.");
        });
    }
}
