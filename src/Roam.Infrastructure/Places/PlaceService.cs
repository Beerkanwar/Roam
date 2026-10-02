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
        // For MVP: Search the local DB first. If not found, use geocoding service.
        var localPlaces = await _context.Places
            .Where(p => p.Name.ToLower().Contains(query.ToLower()))
            .OrderBy(p => p.Name)
            .Take(10)
            .ToListAsync(cancellationToken);

        if (localPlaces.Any())
        {
            return localPlaces;
        }

        var geoResults = await _geocodingService.GeocodeAsync(query, cancellationToken);
        
        var newPlaces = geoResults.Select(g => new Place
        {
            Name = g.Name,
            Description = g.Description,
            Location = new NetTopologySuite.Geometries.Point(g.Longitude, g.Latitude) { SRID = 4326 }
        }).ToList();

        if (newPlaces.Any())
        {
            _context.Places.AddRange(newPlaces);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return newPlaces;
    }

    public async Task<Place?> GetPlaceByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _context.Places.FindAsync(new object[] { id }, cancellationToken);
    }
}
