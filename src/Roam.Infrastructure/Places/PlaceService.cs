using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Roam.Application.Places;
using Roam.Domain.Places;
using Roam.Infrastructure.Persistence;

namespace Roam.Infrastructure.Places;

public class PlaceService : IPlaceService
{
    private const string LikeEscapeCharacter = "\\";

    private readonly ApplicationDbContext _context;
    private readonly IGeocodingService _geocodingService;
    private readonly ILogger<PlaceService>? _logger;

    public PlaceService(ApplicationDbContext context, IGeocodingService geocodingService, ILogger<PlaceService>? logger = null)
    {
        _context = context;
        _geocodingService = geocodingService;
        _logger = logger;
    }

    /// <summary>Escapes LIKE/ILIKE metacharacters so user input is matched literally.</summary>
    internal static string EscapeLikePattern(string value) =>
        value.Replace(LikeEscapeCharacter, LikeEscapeCharacter + LikeEscapeCharacter)
             .Replace("%", LikeEscapeCharacter + "%")
             .Replace("_", LikeEscapeCharacter + "_");

    public async Task<IEnumerable<Place>> SearchPlacesAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<Place>();

        var normalizedQuery = query.Trim().ToLowerInvariant();

        // 1. Search local DB first with trigram index support (user input is escaped so that
        //    '%' and '_' are matched literally instead of acting as wildcards)
        var containsPattern = $"%{EscapeLikePattern(normalizedQuery)}%";
        var localPlaces = await _context.Places
            .Where(p => EF.Functions.ILike(p.Name, containsPattern, LikeEscapeCharacter))
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
            var exactNamePattern = EscapeLikePattern(g.Name);
            var existing = await _context.Places.FirstOrDefaultAsync(
                p => EF.Functions.ILike(p.Name, exactNamePattern, LikeEscapeCharacter) && p.Location.IsWithinDistance(point, 0.001),
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

        // Only places that were actually persisted may be returned: clients use the returned IDs to
        // create tour requests, and an ID that does not exist in the database fails that flow.
        var persistedNewPlaces = new List<Place>();
        if (newPlaces.Count > 0)
        {
            var placesToInsert = newPlaces.Take(5).ToList();
            try
            {
                _context.Places.AddRange(placesToInsert);
                await _context.SaveChangesAsync(cancellationToken);
                persistedNewPlaces = placesToInsert;
            }
            catch (DbUpdateException ex)
            {
                // Gracefully degrade: do not fail read-search if persisting external places fails,
                // but stop tracking the failed inserts so they cannot leak into later saves.
                foreach (var place in placesToInsert)
                {
                    _context.Entry(place).State = EntityState.Detached;
                }
                _logger?.LogWarning(ex, "Failed to persist {Count} geocoded places for search query.", placesToInsert.Count);
            }
        }

        // Merge local, existing geocoded matches, and newly added places, deduplicated by Id
        var combined = localPlaces
            .Concat(matchedExistingPlaces)
            .Concat(persistedNewPlaces)
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
