using Microsoft.AspNetCore.Mvc;
using Roam.Application.TourRequests;

namespace Roam.Api.Endpoints;

public static class TourRequestEndpoints
{
    public static void MapTourRequestEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/api/v1/tour-requests")
            .WithTags("TourRequests")
            .WithApiVersionSet(builder.NewApiVersionSet().Build());

        group.MapPost("/", async ([FromBody] CreateTourRequestDto dto, ITourRequestService service, CancellationToken cancellationToken) =>
        {
            // Usually RequesterId comes from auth context, stubbing for MVP
            if (string.IsNullOrEmpty(dto.RequesterId))
                dto.RequesterId = "user_" + Guid.NewGuid().ToString("N")[..8];

            var request = await service.CreateRequestAsync(dto, cancellationToken);
            return Results.Created($"/api/v1/tour-requests/{request.Id}", request);
        });

        group.MapPost("/coordinates", async ([FromBody] CreateTourRequestByCoordinatesDto dto, ITourRequestService service, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrEmpty(dto.RequesterId))
                dto.RequesterId = "user_" + Guid.NewGuid().ToString("N")[..8];

            var request = await service.CreateRequestByCoordinatesAsync(dto, cancellationToken);
            return Results.Created($"/api/v1/tour-requests/{request.Id}", request);
        });

        group.MapPost("/{id}/accept", async (string id, [FromQuery] string volunteerId, ITourRequestService service, CancellationToken cancellationToken) =>
        {
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
