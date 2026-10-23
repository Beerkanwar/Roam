using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Roam.Application.Sessions;

namespace Roam.Infrastructure.Realtime;

public class TurnService : ITurnService
{
    private readonly TurnServerOptions _options;

    public TurnService(IOptions<TurnServerOptions> options)
    {
        _options = options.Value;
    }

    public Task<IEnumerable<IceServerDto>> GetIceServersAsync(string userId, CancellationToken cancellationToken = default)
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

        var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(username));
        var credential = Convert.ToBase64String(hash);

        var servers = new List<IceServerDto>
        {
            new IceServerDto
            {
                Urls = _options.Uris,
                Username = username,
                Credential = credential
            }
        };

        return Task.FromResult<IEnumerable<IceServerDto>>(servers);
    }
}
