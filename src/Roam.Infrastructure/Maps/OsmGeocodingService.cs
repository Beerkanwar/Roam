using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Roam.Application.Places;

namespace Roam.Infrastructure.Maps;

public class OsmGeocodingService : IGeocodingService
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string? _apiKey;

    public OsmGeocodingService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _endpoint = configuration["Geocoding:Endpoint"] ?? "https://nominatim.openstreetmap.org/search";
        _apiKey = configuration["Geocoding:ApiKey"];

        var userAgent = configuration["Geocoding:UserAgent"] ?? "RoamApp/1.0 (https://github.com/Beerkanwar/Roam; support@roam.local)";
        _httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
    }

    public async Task<IEnumerable<GeocodingResult>> GeocodeAsync(string query, CancellationToken cancellationToken = default)
    {
        var url = $"{_endpoint}?q={Uri.EscapeDataString(query)}&format=json&limit=5";
        if (!string.IsNullOrWhiteSpace(_apiKey) && !_apiKey.Contains("dummy"))
        {
            url += $"&key={Uri.EscapeDataString(_apiKey)}";
        }
        
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
