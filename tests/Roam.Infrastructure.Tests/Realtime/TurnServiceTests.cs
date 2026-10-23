using Microsoft.Extensions.Options;
using Roam.Application.Sessions;
using Roam.Infrastructure.Realtime;
using Xunit;

namespace Roam.Infrastructure.Tests.Realtime;

public class TurnServiceTests
{
    [Fact]
    public async Task GetIceServersAsync_ThrowsInvalidOperationException_WhenSecretMissing()
    {
        // Arrange
        var options = Options.Create(new TurnServerOptions
        {
            Uris = new[] { "turn:127.0.0.1:3478" },
            Password = string.Empty
        });
        Environment.SetEnvironmentVariable("TURN_SECRET", null);
        var service = new TurnService(options);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetIceServersAsync("user-123"));
        Assert.Contains("TURN server credentials are not configured", ex.Message);
    }

    [Fact]
    public async Task GetIceServersAsync_GeneratesHmacCredential_WhenConfigured()
    {
        // Arrange
        var options = Options.Create(new TurnServerOptions
        {
            Uris = new[] { "turn:127.0.0.1:3478" },
            Password = "test-secret-key-12345"
        });
        var service = new TurnService(options);

        // Act
        var servers = (await service.GetIceServersAsync("user-456")).ToList();

        // Assert
        Assert.Single(servers);
        var server = servers[0];
        Assert.Equal("turn:127.0.0.1:3478", server.Urls[0]);
        Assert.Contains(":user-456", server.Username);
        Assert.False(string.IsNullOrWhiteSpace(server.Credential));
    }
}
