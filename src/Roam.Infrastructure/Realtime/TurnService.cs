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
        // For standard coturn REST API, we generate a short-lived credential
        var ttlSeconds = 86400; // 24 hours
        var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ttlSeconds;
        var username = $"{unixTimestamp}:{userId}";

        var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(_options.Password));
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
