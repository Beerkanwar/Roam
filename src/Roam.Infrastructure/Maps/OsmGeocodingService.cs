using System.Text.Json;
using Roam.Application.Places;

namespace Roam.Infrastructure.Maps;

public class OsmGeocodingService : IGeocodingService
{
    private readonly HttpClient _httpClient;

    public OsmGeocodingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "RoamApp/1.0");
    }

    public async Task<IEnumerable<GeocodingResult>> GeocodeAsync(string query, CancellationToken cancellationToken = default)
    {
        // Example: https://nominatim.openstreetmap.org/search?q=query&format=json&limit=5
        var url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(query)}&format=json&limit=5";
        
        var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return Enumerable.Empty<GeocodingResult>();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = JsonDocument.Parse(content);

        var results = new List<GeocodingResult>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.TryGetProperty("display_name", out var nameProp) &&
                item.TryGetProperty("lat", out var latProp) &&
                item.TryGetProperty("lon", out var lonProp))
            {
                if (double.TryParse(latProp.GetString(), out var lat) && 
                    double.TryParse(lonProp.GetString(), out var lon))
                {
                    results.Add(new GeocodingResult
                    {
                        Name = nameProp.GetString()?.Split(',').FirstOrDefault() ?? "Unknown",
                        Description = nameProp.GetString() ?? "",
                        Latitude = lat,
                        Longitude = lon
                    });
                }
            }
        }

        return results;
    }
}
