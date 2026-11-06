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
        Assert.Equal(TimeSpan.FromSeconds(10), httpClient.Timeout);
    }

    private class TimeoutHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    [Fact]
    public async Task OsmGeocodingService_WhenRequestTimesOut_ReturnsEmptyResultsGracefully()
    {
        var config = new ConfigurationBuilder().Build();
        var httpClient = new HttpClient(new TimeoutHandler());
        var service = new OsmGeocodingService(httpClient, config);

        var results = await service.GeocodeAsync("TimeoutTestLocation");

        Assert.NotNull(results);
        Assert.Empty(results);
    }
}
