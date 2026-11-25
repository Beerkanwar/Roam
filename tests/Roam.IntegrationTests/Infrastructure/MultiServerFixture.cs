using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Roam.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace Roam.IntegrationTests.Infrastructure;

/// <summary>One API instance in a multi-server deployment: its own host, shared PostgreSQL and Redis.</summary>
public sealed class RoamServerFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseConnection;
    private readonly string _redisConnection;

    public RoamServerFactory(string databaseConnection, string redisConnection)
    {
        _databaseConnection = databaseConnection;
        _redisConnection = redisConnection;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", _databaseConnection);
        builder.UseSetting("ConnectionStrings:Redis", _redisConnection);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
            services.RemoveAll(typeof(ApplicationDbContext));
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(_databaseConnection, x => x.UseNetTopologySuite()));
        });
    }
}

/// <summary>Two API servers ("A" and "B") sharing one PostgreSQL database and one Redis instance.</summary>
public sealed class MultiServerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgis/postgis:15-3.3")
        .WithDatabase("roam_multi")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    public RoamServerFactory ServerA { get; private set; } = null!;
    public RoamServerFactory ServerB { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_db.StartAsync(), _redis.StartAsync());

        ServerA = new RoamServerFactory(_db.GetConnectionString(), _redis.GetConnectionString());
        ServerB = new RoamServerFactory(_db.GetConnectionString(), _redis.GetConnectionString());

        // Start both at once, like a rolling deployment: migrations and seeding must not race.
        await Task.WhenAll(
            Task.Run(() => ServerA.Server),
            Task.Run(() => ServerB.Server));
    }

    public async Task DisposeAsync()
    {
        await ServerA.DisposeAsync();
        await ServerB.DisposeAsync();
        await _db.DisposeAsync();
        await _redis.DisposeAsync();
    }
}
