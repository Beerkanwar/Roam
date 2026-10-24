using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Roam.Application.Places;

namespace Roam.Infrastructure.Maps;

public class OsmGeocodingService : IGeocodingService
{
    private static readonly SemaphoreSlim _throttle = new(1, 1);
    private static DateTime _lastRequestTime = DateTime.MinValue;
    private static readonly ConcurrentDictionary<string, (DateTime Expiry, List<GeocodingResult> Results)> _cache = new();

    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string? _apiKey;

    public OsmGeocodingService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _endpoint = configuration["Geocoding:Endpoint"] ?? "https://nominatim.openstreetmap.org/search";
        _apiKey = configuration["Geocoding:ApiKey"];

        var userAgent = configuration["Geocoding:UserAgent"] ?? "RoamApp/1.0 (https://github.com/Beerkanwar/Roam; contact: github.com/Beerkanwar/Roam)";
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
        }
    }

    public async Task<IEnumerable<GeocodingResult>> GeocodeAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Enumerable.Empty<GeocodingResult>();

        var normalizedKey = query.Trim().ToLowerInvariant();

        // 1. Check in-memory cache
        if (_cache.TryGetValue(normalizedKey, out var cachedEntry) && cachedEntry.Expiry > DateTime.UtcNow)
        {
            return cachedEntry.Results;
        }

        // 2. Global Nominatim rate throttle (maximum 1 request per second as per OSM usage policy)
        await _throttle.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(normalizedKey, out cachedEntry) && cachedEntry.Expiry > DateTime.UtcNow)
            {
                return cachedEntry.Results;
            }

            var timeSinceLast = DateTime.UtcNow - _lastRequestTime;
            if (timeSinceLast < TimeSpan.FromSeconds(1))
            {
                await Task.Delay(TimeSpan.FromSeconds(1) - timeSinceLast, cancellationToken);
            }

            var url = $"{_endpoint}?q={Uri.EscapeDataString(query)}&format=json&limit=5";
            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                url += $"&key={Uri.EscapeDataString(_apiKey)}";
            }
            
            var response = await _httpClient.GetAsync(url, cancellationToken);
            _lastRequestTime = DateTime.UtcNow;

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

            _cache[normalizedKey] = (DateTime.UtcNow.AddHours(24), results);
            return results;
        }
        finally
        {
            _throttle.Release();
        }
    }
}
