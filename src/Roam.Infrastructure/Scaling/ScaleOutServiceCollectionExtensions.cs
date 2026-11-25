using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Roam.Infrastructure.Scaling;

public static class ScaleOutServiceCollectionExtensions
{
    /// <summary>Upper bound for the in-memory fallback cache used when Redis is not configured.</summary>
    private const long InMemoryCacheSizeLimitBytes = 64L * 1024 * 1024;

    /// <summary>
    /// Registers the state shared between API instances. With a Redis connection string, a single
    /// <see cref="IConnectionMultiplexer"/> backs the distributed cache (and is reused by SignalR and rate
    /// limiting). Without one, an in-memory cache is used, which is correct for a single instance only.
    /// Returns the multiplexer, or null when running single-instance.
    /// </summary>
    public static IConnectionMultiplexer? AddRoamScaleOut(this IServiceCollection services, string? redisConnectionString)
    {
        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddDistributedMemoryCache(options => options.SizeLimit = InMemoryCacheSizeLimitBytes);
            return null;
        }

        var options = ConfigurationOptions.Parse(redisConnectionString);
        // Keep starting (and retrying in the background) if Redis is briefly unavailable.
        options.AbortOnConnectFail = false;
        var multiplexer = ConnectionMultiplexer.Connect(options);

        services.AddSingleton<IConnectionMultiplexer>(multiplexer);
        services.AddStackExchangeRedisCache(cacheOptions =>
        {
            cacheOptions.ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(multiplexer);
            cacheOptions.InstanceName = "roam:cache:";
        });

        return multiplexer;
    }
}
