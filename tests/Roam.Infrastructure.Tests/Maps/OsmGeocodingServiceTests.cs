using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Roam.Infrastructure.Maps;
using Xunit;

namespace Roam.Infrastructure.Tests.Maps;

public class OsmGeocodingServiceTests
{
    private const string ValidPayload =
        """[{"display_name":"Eiffel Tower, Paris, France","lat":"48.8583701","lon":"2.2944813"}]""";

    [Fact]
    public void ParseResults_ParsesCoordinatesCultureInvariantly()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            // In de-DE '.' is the group separator; culture-sensitive parsing would turn 48.858 into 48858...
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var results = OsmGeocodingService.ParseResults(ValidPayload);

            var result = Assert.Single(results);
            Assert.Equal("Eiffel Tower", result.Name);
            Assert.Equal(48.8583701, result.Latitude, 6);
            Assert.Equal(2.2944813, result.Longitude, 6);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData("<html>Service temporarily unavailable</html>")]
    [InlineData("{\"error\":\"rate limited\"}")]
    [InlineData("")]
    [InlineData("[1, \"x\", null]")]
    [InlineData("[{\"display_name\":\"Nowhere\",\"lat\":\"123.0\",\"lon\":\"10.0\"}]")]
    [InlineData("[{\"display_name\":\"Nowhere\",\"lat\":48.1,\"lon\":10.0}]")]
    public void ParseResults_ReturnsEmpty_ForMalformedOrInvalidPayloads(string payload)
    {
        Assert.Empty(OsmGeocodingService.ParseResults(payload));
    }

    [Fact]
    public async Task GeocodeAsync_DoesNotThrow_WhenProviderReturnsNonJsonSuccess()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "<html>maintenance</html>");
        var service = new OsmGeocodingService(new HttpClient(handler), new ConfigurationBuilder().Build());

        var results = await service.GeocodeAsync($"non-json-{Guid.NewGuid()}");

        Assert.Empty(results);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task GeocodeAsync_ReturnsParsedResults_ForValidResponse()
    {
        var handler = new StubHandler(HttpStatusCode.OK, ValidPayload);
        var service = new OsmGeocodingService(new HttpClient(handler), new ConfigurationBuilder().Build());

        var results = (await service.GeocodeAsync($"eiffel-{Guid.NewGuid()}")).ToList();

        Assert.Single(results);
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
