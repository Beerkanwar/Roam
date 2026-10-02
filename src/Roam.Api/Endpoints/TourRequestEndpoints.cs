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

        group.MapPost("/{id}/schedule/propose", async (string id, [FromBody] ProposeScheduleDto dto, ClaimsPrincipal user, ITourRequestService service, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User ID missing from token.");
            try
            {
                var schedule = await service.ProposeScheduleAsync(id, userId, dto.ProposedStartUtc, dto.ProposedEndUtc, cancellationToken);
                return Results.Ok(schedule);
            }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
        });

        group.MapPost("/schedule/{scheduleId}/accept", async (string scheduleId, ClaimsPrincipal user, ITourRequestService service, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User ID missing from token.");
            try
            {
                var success = await service.AcceptScheduleAsync(scheduleId, userId, cancellationToken);
                return success ? Results.Ok() : Results.BadRequest("Cannot accept schedule.");
            }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
        });

        group.MapPost("/schedule/{scheduleId}/reject", async (string scheduleId, ClaimsPrincipal user, ITourRequestService service, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User ID missing from token.");
            try
            {
                var success = await service.RejectScheduleAsync(scheduleId, userId, cancellationToken);
                return success ? Results.Ok() : Results.BadRequest("Cannot reject schedule.");
            }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
        });

        group.MapGet("/", async (ClaimsPrincipal user, ITourRequestService service, [FromQuery] double radius = 5000, CancellationToken cancellationToken = default) =>
        {
            var volunteerId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User ID missing from token.");

            var requests = await service.GetNearbyRequestsAsync(volunteerId, radius, cancellationToken);
            
            // Map the requests to a safe response DTO to avoid cyclic references or returning unnecessary internal fields
            var response = requests.Select(r => new
            {
                r.Id,
                r.RequesterId,
                r.Description,
                r.Mode,
                r.Visibility,
                Place = new 
                {
                    r.Place.Id,
                    r.Place.Name,
                    r.Place.Description,
                    Latitude = r.Place.Location.Y,
                    Longitude = r.Place.Location.X
                }
            });

            return Results.Ok(response);
        });

        group.MapPost("/{id}/cancel", async (string id, [FromBody] CancelRequestDto dto, ClaimsPrincipal user, ITourRequestService service, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User ID missing from token.");
            try
            {
                var success = await service.CancelRequestAsync(id, userId, dto.Reason, cancellationToken);
                return success ? Results.Ok() : Results.NotFound();
            }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
        });

        group.MapPost("/{id}/start", async (string id, ClaimsPrincipal user, ITourRequestService service, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User ID missing from token.");
            try
            {
                var success = await service.StartTourAsync(id, userId, cancellationToken);
                return success ? Results.Ok() : Results.NotFound();
            }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
        });

        group.MapPost("/{id}/complete", async (string id, ClaimsPrincipal user, ITourRequestService service, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User ID missing from token.");
            try
            {
                var success = await service.CompleteTourAsync(id, userId, cancellationToken);
                return success ? Results.Ok() : Results.NotFound();
            }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
        });

        group.MapPost("/{id}/interest", async (string id, ClaimsPrincipal user, ITourRequestService service, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User ID missing from token.");
            try
            {
                var success = await service.MarkVolunteerInterestedAsync(id, userId, cancellationToken);
                return success ? Results.Ok() : Results.NotFound();
            }
            catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
        });
    }
}

public class CancelRequestDto
{
    public string Reason { get; set; } = string.Empty;
}

public class ProposeScheduleDto
{
    public DateTime ProposedStartUtc { get; set; }
    public DateTime? ProposedEndUtc { get; set; }
}
