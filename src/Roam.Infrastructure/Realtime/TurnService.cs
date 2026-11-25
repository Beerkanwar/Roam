using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Roam.Application.Sessions;

namespace Roam.Infrastructure.Realtime;

public class TurnService : ITurnService
{
    // Cloudflare credentials must outlive the longest tour; the server-side session timeout is a few hours.
    private const int CloudflareTtlSeconds = 6 * 60 * 60;
    private const string CloudflareApiBase = "https://rtc.live.cloudflare.com/v1/turn/keys/";

    private readonly TurnServerOptions _options;
    private readonly HttpClient? _httpClient;
    private readonly ILogger _logger;

    public TurnService(IOptions<TurnServerOptions> options, HttpClient? httpClient = null, ILogger<TurnService>? logger = null)
    {
        _options = options.Value;
        _httpClient = httpClient;
        _logger = (ILogger?)logger ?? NullLogger.Instance;
    }

    public async Task<IEnumerable<IceServerDto>> GetIceServersAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.Equals(_options.Provider, "cloudflare", StringComparison.OrdinalIgnoreCase))
        {
            return await GetCloudflareIceServersAsync(cancellationToken);
        }

        if (_options.Uris.Length == 0)
        {
            // No relay configured: STUN lets most peers connect directly; strict NATs will need TURN.
            return StunOnly();
        }

        return GetCoturnIceServers(userId);
    }

    private IEnumerable<IceServerDto> StunOnly() =>
        _options.StunUris.Length == 0
            ? Array.Empty<IceServerDto>()
            : new[] { new IceServerDto { Urls = _options.StunUris } };

    private IEnumerable<IceServerDto> GetCoturnIceServers(string userId)
    {
        // Generate short-lived credential with 10-minute TTL
        var ttlSeconds = 600; // 10 minutes
        var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ttlSeconds;
        var username = $"{unixTimestamp}:{userId}";

        var secret = !string.IsNullOrWhiteSpace(_options.Password)
            ? _options.Password
            : Environment.GetEnvironmentVariable("TURN_SECRET");

        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("TURN server credentials are not configured. Set TurnServer:Password or TURN_SECRET environment variable.");
        }

        // coturn's REST API (use-auth-secret) mandates HMAC-SHA1 for the ephemeral credential.
        var hash = HMACSHA1.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(username));
        var credential = Convert.ToBase64String(hash);

        return new List<IceServerDto>
        {
            new IceServerDto
            {
                Urls = _options.Uris,
                Username = username,
                Credential = credential
            }
        };
    }

    private async Task<IEnumerable<IceServerDto>> GetCloudflareIceServersAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.CloudflareKeyId) || string.IsNullOrWhiteSpace(_options.CloudflareApiToken))
        {
            throw new InvalidOperationException("Cloudflare TURN is selected but TurnServer:CloudflareKeyId / TurnServer:CloudflareApiToken are not set.");
        }
        if (_httpClient is null)
        {
            throw new InvalidOperationException("Cloudflare TURN requires an HttpClient.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"{CloudflareApiBase}{Uri.EscapeDataString(_options.CloudflareKeyId)}/credentials/generate-ice-servers")
        {
            Content = JsonContent.Create(new { ttl = CloudflareTtlSeconds })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.CloudflareApiToken);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Cloudflare TURN credential request failed with HTTP {Status}.", (int)response.StatusCode);
                return StunOnly();
            }

            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var servers = ParseCloudflareIceServers(json.RootElement).ToList();
            return servers.Count > 0 ? servers : StunOnly();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException && !cancellationToken.IsCancellationRequested)
        {
            // A relay outage should degrade to direct connections, not break joining a tour.
            _logger.LogError(ex, "Cloudflare TURN credential request failed.");
            return StunOnly();
        }
    }

    /// <summary>Accepts both the array form ({"iceServers":[...]}) and the older single-object form.</summary>
    internal static IEnumerable<IceServerDto> ParseCloudflareIceServers(JsonElement root)
    {
        if (!root.TryGetProperty("iceServers", out var iceServers))
            yield break;

        var entries = iceServers.ValueKind == JsonValueKind.Array
            ? iceServers.EnumerateArray().ToList()
            : new List<JsonElement> { iceServers };

        foreach (var entry in entries)
        {
            if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("urls", out var urlsElement))
                continue;

            var urls = (urlsElement.ValueKind == JsonValueKind.Array
                    ? urlsElement.EnumerateArray().Select(u => u.GetString())
                    : new[] { urlsElement.GetString() })
                .Where(u => !string.IsNullOrWhiteSpace(u))
                // Browsers block port 53; without trickle ICE those URLs only add a timeout.
                .Where(u => !u!.Contains(":53?") && !u.EndsWith(":53"))
                .Select(u => u!)
                .ToArray();
            if (urls.Length == 0)
                continue;

            yield return new IceServerDto
            {
                Urls = urls,
                Username = entry.TryGetProperty("username", out var user) ? user.GetString() ?? string.Empty : string.Empty,
                Credential = entry.TryGetProperty("credential", out var cred) ? cred.GetString() ?? string.Empty : string.Empty
            };
        }
    }
}
