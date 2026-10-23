using Microsoft.Extensions.Configuration;
using Roam.Infrastructure.Maps;
using Xunit;

namespace Roam.Infrastructure.Tests;

public class BasicInfrastructureTests
{
    [Fact]
    public void OsmGeocodingService_InitializesWithDefaultConfiguration()
    {
        var config = new ConfigurationBuilder().Build();
        var httpClient = new HttpClient();
        var service = new OsmGeocodingService(httpClient, config);

        Assert.NotNull(service);
        Assert.True(httpClient.DefaultRequestHeaders.Contains("User-Agent"));
    }
}
