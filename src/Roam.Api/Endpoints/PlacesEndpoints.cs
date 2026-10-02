using Microsoft.AspNetCore.Mvc;
using Roam.Application.Places;

namespace Roam.Api.Endpoints;

public static class PlacesEndpoints
{
    public record PlaceDto(string Id, string Name, string Description, double Latitude, double Longitude);

    public static void MapPlacesEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/api/v1/places")
            .WithTags("Places")
            .WithApiVersionSet(builder.NewApiVersionSet().Build())
            .RequireAuthorization();

        group.MapGet("/search", async ([FromQuery] string q, IPlaceService placeService, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(q))
                return Results.BadRequest("Search query 'q' is required.");

            var places = await placeService.SearchPlacesAsync(q, cancellationToken);
            var dtos = places.Select(p => new PlaceDto(p.Id, p.Name, p.Description, p.Location.Y, p.Location.X));
            return Results.Ok(dtos);
        });

        group.MapGet("/{id}", async (string id, IPlaceService placeService, CancellationToken cancellationToken) =>
        {
            var place = await placeService.GetPlaceByIdAsync(id, cancellationToken);
            if (place is null) return Results.NotFound();
            return Results.Ok(new PlaceDto(place.Id, place.Name, place.Description, place.Location.Y, place.Location.X));
        });
    }
}
