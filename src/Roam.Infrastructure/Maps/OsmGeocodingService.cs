using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Roam.Application.Places;
using StackExchange.Redis;

namespace Roam.Infrastructure.Maps;

public class OsmGeocodingService : IGeocodingService
{
    private const int MaxCacheEntries = 1000;
    private static readonly SemaphoreSlim _throttle = new(1, 1);
    private static DateTime _lastRequestTime = DateTime.MinValue;
    private static readonly ConcurrentDictionary<string, (DateTime Expiry, List<GeocodingResult> Results)> _cache = new();

    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(1);
    private const string GlobalThrottleKey = "roam:geocode:throttle";

    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string? _apiKey;
    private readonly IDistributedCache? _sharedCache;
    private readonly IConnectionMultiplexer? _redis;

    /// <param name="sharedCache">Cache shared by all API instances (Redis in multi-server deployments).</param>
    /// <param name="redis">When present, the 1 request/second Nominatim limit is enforced across all instances.</param>
    public OsmGeocodingService(HttpClient httpClient, IConfiguration configuration,
        IDistributedCache? sharedCache = null, IConnectionMultiplexer? redis = null)
    {
        _httpClient = httpClient;
        _sharedCache = sharedCache;
        _redis = redis;
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
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

        // 1. Check cache
        var cached = await TryGetCachedAsync(normalizedKey, cancellationToken);
        if (cached != null)
            return cached;

        // 2. Nominatim rate throttle (maximum 1 request per second as per OSM usage policy):
        //    serialized within this process, and across processes via Redis when configured.
        await _throttle.WaitAsync(cancellationToken);
        try
        {
            cached = await TryGetCachedAsync(normalizedKey, cancellationToken);
            if (cached != null)
                return cached;

            var timeSinceLast = DateTime.UtcNow - _lastRequestTime;
            if (timeSinceLast < MinInterval)
            {
                await Task.Delay(MinInterval - timeSinceLast, cancellationToken);
            }

            await WaitForGlobalSlotAsync(cancellationToken);

            var url = $"{_endpoint}?q={Uri.EscapeDataString(query)}&format=json&limit=5";
            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                url += $"&key={Uri.EscapeDataString(_apiKey)}";
            }
            
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.GetAsync(url, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Enumerable.Empty<GeocodingResult>();
            }
            catch (HttpRequestException)
            {
                return Enumerable.Empty<GeocodingResult>();
            }
            finally
            {
                // Count failed attempts too, so errors do not bypass the 1 request/second policy.
                _lastRequestTime = DateTime.UtcNow;
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                    return Enumerable.Empty<GeocodingResult>();

                string content;
                try
                {
                    content = await response.Content.ReadAsStringAsync(timeoutCts.Token);
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    return Enumerable.Empty<GeocodingResult>();
                }

                var results = ParseResults(content);
                await StoreCachedAsync(normalizedKey, results, cancellationToken);
                return results;
            }
        }
        finally
        {
            _throttle.Release();
        }
    }

    /// <summary>
    /// Parses a Nominatim JSON response. Malformed or unexpected payloads yield no results instead of
    /// throwing, and coordinates are parsed culture-invariantly and range-checked.
    /// </summary>
    internal static List<GeocodingResult> ParseResults(string content)
    {
        var results = new List<GeocodingResult>();
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(content);
        }
        catch (JsonException)
        {
            return results;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return results;

            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object &&
                    item.TryGetProperty("display_name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String &&
                    item.TryGetProperty("lat", out var latProp) && latProp.ValueKind == JsonValueKind.String &&
                    item.TryGetProperty("lon", out var lonProp) && lonProp.ValueKind == JsonValueKind.String &&
                    double.TryParse(latProp.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) &&
                    double.TryParse(lonProp.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) &&
                    lat is >= -90 and <= 90 && lon is >= -180 and <= 180)
                {
                    var displayName = nameProp.GetString() ?? string.Empty;
                    var name = displayName.Split(',').FirstOrDefault()?.Trim();
                    results.Add(new GeocodingResult
                    {
                        Name = string.IsNullOrEmpty(name) ? "Unknown" : name,
                        Description = displayName,
                        Latitude = lat,
                        Longitude = lon
                    });
                }
            }
        }

        return results;
    }

    private async Task<List<GeocodingResult>?> TryGetCachedAsync(string key, CancellationToken cancellationToken)
    {
        if (_sharedCache != null)
        {
            try
            {
                var bytes = await _sharedCache.GetAsync(CacheKey(key), cancellationToken);
                return bytes == null ? null : JsonSerializer.Deserialize<List<GeocodingResult>>(bytes);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return null; // Cache outage must not break search.
            }
        }

        return _cache.TryGetValue(key, out var entry) && entry.Expiry > DateTime.UtcNow ? entry.Results : null;
    }

    private async Task StoreCachedAsync(string key, List<GeocodingResult> results, CancellationToken cancellationToken)
    {
        if (_sharedCache != null)
        {
            try
            {
                await _sharedCache.SetAsync(CacheKey(key), JsonSerializer.SerializeToUtf8Bytes(results),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheDuration }, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Best effort.
            }
            return;
        }

        StoreInLocalCache(key, results);
    }

    private static string CacheKey(string key) => $"geocode:{key}";

    /// <summary>Claims the single cluster-wide "one request per second" slot, waiting for it if needed.</summary>
    private async Task WaitForGlobalSlotAsync(CancellationToken cancellationToken)
    {
        if (_redis == null) return;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            var db = _redis.GetDatabase();
            while (!await db.StringSetAsync(GlobalThrottleKey, 1, MinInterval, When.NotExists))
            {
                var remaining = await db.KeyTimeToLiveAsync(GlobalThrottleKey) ?? TimeSpan.FromMilliseconds(50);
                await Task.Delay(remaining > TimeSpan.Zero ? remaining : TimeSpan.FromMilliseconds(50), timeout.Token);
            }
        }
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException or RedisConnectionException)
        {
            // Redis unavailable: the per-process throttle above still applies.
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Waited too long for a slot; proceed rather than fail the search.
        }
    }

    private static void StoreInLocalCache(string key, List<GeocodingResult> results)
    {
        // Queries are user-controlled, so bound the static cache to prevent unbounded memory growth.
        if (_cache.Count >= MaxCacheEntries)
        {
            var now = DateTime.UtcNow;
            foreach (var entry in _cache)
            {
                if (entry.Value.Expiry <= now)
                    _cache.TryRemove(entry.Key, out _);
            }

            if (_cache.Count >= MaxCacheEntries)
                _cache.Clear();
        }

        _cache[key] = (DateTime.UtcNow.Add(CacheDuration), results);
    }
}
