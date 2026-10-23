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

        // 1. Search local DB first
        var localPlaces = await _context.Places
            .Where(p => p.Name.ToLower().Contains(normalizedQuery))
            .OrderBy(p => p.Name)
            .Take(10)
            .ToListAsync(cancellationToken);

        if (localPlaces.Count > 0)
        {
            return localPlaces;
        }

        // 2. Fetch external geocoding results
        var geoResults = await _geocodingService.GeocodeAsync(query, cancellationToken);
        var newPlaces = new List<Place>();

        foreach (var g in geoResults)
        {
            var point = new NetTopologySuite.Geometries.Point(g.Longitude, g.Latitude) { SRID = 4326 };

            // Deduplicate: check if place already exists with matching name within ~100 meters
            var exists = await _context.Places.AnyAsync(
                p => p.Name.ToLower() == g.Name.ToLower() && p.Location.IsWithinDistance(point, 0.001), 
                cancellationToken);

            if (!exists)
            {
                newPlaces.Add(new Place
                {
                    Name = g.Name,
                    Description = g.Description,
                    Location = point
                });
            }
        }

        if (newPlaces.Count > 0)
        {
            _context.Places.AddRange(newPlaces);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return newPlaces.Count > 0 ? newPlaces : localPlaces;
    }

    public async Task<Place?> GetPlaceByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _context.Places.FindAsync(new object[] { id }, cancellationToken);
    }
}
