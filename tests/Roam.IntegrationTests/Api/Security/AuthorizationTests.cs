using System.Net;
using System.Net.Http.Headers;
using Roam.IntegrationTests.Infrastructure;
using Xunit;

namespace Roam.IntegrationTests.Api.Security;

public class AuthorizationTests : IClassFixture<IntegrationTestWebAppFactory>
{
    private readonly HttpClient _client;

    public AuthorizationTests(IntegrationTestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("GET", "/api/v1/places/search")]
    [InlineData("POST", "/api/v1/tour-requests")]
    [InlineData("POST", "/api/v1/trust-safety/reports")]
    public async Task ProtectedEndpoints_ReturnUnauthorized_ForAnonymousUsers(string method, string endpoint)
    {
        // Act
        var request = new HttpRequestMessage(new HttpMethod(method), endpoint);
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
