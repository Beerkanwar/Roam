using Microsoft.AspNetCore.Mvc;
using Roam.Application.Places;

namespace Roam.Api.Endpoints;

public static class PlacesEndpoints
{
    public static void MapPlacesEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/api/v1/places")
            .WithTags("Places")
            .WithApiVersionSet(builder.NewApiVersionSet().Build());

        group.MapGet("/search", async ([FromQuery] string q, IPlaceService placeService, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(q))
                return Results.BadRequest("Search query 'q' is required.");

            var places = await placeService.SearchPlacesAsync(q, cancellationToken);
            return Results.Ok(places);
        });

        group.MapGet("/{id}", async (string id, IPlaceService placeService, CancellationToken cancellationToken) =>
        {
            var place = await placeService.GetPlaceByIdAsync(id, cancellationToken);
            return place is not null ? Results.Ok(place) : Results.NotFound();
        });
    }
}
