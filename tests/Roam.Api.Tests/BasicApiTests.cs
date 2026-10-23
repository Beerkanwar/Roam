using Microsoft.Extensions.Configuration;
using Xunit;

namespace Roam.Api.Tests;

public class BasicApiTests
{
    [Fact]
    public void Configuration_LoadsTurnServerSettings()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "TurnServer:Uris:0", "turn:127.0.0.1:3478" },
            { "TurnServer:Password", "roam-test-secret" }
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        Assert.Equal("turn:127.0.0.1:3478", config["TurnServer:Uris:0"]);
        Assert.Equal("roam-test-secret", config["TurnServer:Password"]);
    }
}
