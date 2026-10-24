using Microsoft.EntityFrameworkCore;
using Roam.Application.Places;
using Roam.Domain.Places;
using Roam.Infrastructure.Persistence;

namespace Roam.Infrastructure.Places;

public class PlaceService : IPlaceService
{
    private readonly ApplicationDbContext _context;
    private readonly IGeocodingService _geocodingService;

    public PlaceService(ApplicationDbContext context, IGeocodingService geocodingService)
    {
        _context = context;
        _geocodingService = geocodingService;
    }

    public async Task<IEnumerable<Place>> SearchPlacesAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<Place>();

        var normalizedQuery = query.Trim().ToLowerInvariant();

        // 1. Search local DB first with trigram index support
        var localPlaces = await _context.Places
            .Where(p => EF.Functions.ILike(p.Name, $"%{normalizedQuery}%"))
            .OrderBy(p => p.Name)
            .Take(10)
            .ToListAsync(cancellationToken);

        // If local results are sufficient, return them directly
        if (localPlaces.Count >= 5)
        {
            return localPlaces;
        }

        // 2. Fetch external geocoding results to supplement local places
        var geoResults = await _geocodingService.GeocodeAsync(query, cancellationToken);
        var newPlaces = new List<Place>();
        var matchedExistingPlaces = new List<Place>();

        foreach (var g in geoResults)
        {
            var point = new NetTopologySuite.Geometries.Point(g.Longitude, g.Latitude) { SRID = 4326 };

            // Deduplicate: check if place already exists with matching name within ~100 meters
            var existing = await _context.Places.FirstOrDefaultAsync(
                p => EF.Functions.ILike(p.Name, g.Name) && p.Location.IsWithinDistance(point, 0.001), 
                cancellationToken);

            if (existing == null)
            {
                var newPlace = new Place
                {
                    Name = g.Name,
                    Description = g.Description,
                    Location = point
                };
                newPlaces.Add(newPlace);
            }
            else
            {
                matchedExistingPlaces.Add(existing);
            }
        }

        if (newPlaces.Count > 0)
        {
            _context.Places.AddRange(newPlaces);
            await _context.SaveChangesAsync(cancellationToken);
        }

        // Merge local, existing geocoded matches, and newly added places, deduplicated by Id
        var combined = localPlaces
            .Concat(matchedExistingPlaces)
            .Concat(newPlaces)
            .GroupBy(p => p.Id)
            .Select(g => g.First())
            .Take(10)
            .ToList();

        return combined;
    }

    public async Task<Place?> GetPlaceByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _context.Places.FindAsync(new object[] { id }, cancellationToken);
    }
}
