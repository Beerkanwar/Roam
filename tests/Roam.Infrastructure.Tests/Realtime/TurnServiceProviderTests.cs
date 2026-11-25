using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Roam.Application.Sessions;
using Roam.Infrastructure.Realtime;
using Xunit;

namespace Roam.Infrastructure.Tests.Realtime;

public class TurnServiceProviderTests
{
    private const string CloudflareResponse = """
        {"iceServers":[
          {"urls":["stun:stun.cloudflare.com:3478","stun:stun.cloudflare.com:53"]},
          {"urls":["turn:turn.cloudflare.com:3478?transport=udp","turn:turn.cloudflare.com:53?transport=udp","turns:turn.cloudflare.com:443?transport=tcp"],
           "username":"cf-user","credential":"cf-credential"}
        ]}
        """;

    [Fact]
    public async Task NoRelayConfigured_ReturnsStunOnly_InsteadOfFailing()
    {
        var service = new TurnService(Options.Create(new TurnServerOptions()));

        var servers = (await service.GetIceServersAsync("user-1")).ToList();

        var server = Assert.Single(servers);
        Assert.Equal(new[] { "stun:stun.cloudflare.com:3478" }, server.Urls);
        Assert.Equal(string.Empty, server.Credential);
    }

    [Fact]
    public async Task Cloudflare_RequestsCredentialsWithBearerToken_AndDropsPort53Urls()
    {
        var handler = new StubHandler(HttpStatusCode.Created, CloudflareResponse);
        var service = new TurnService(CloudflareOptions(), new HttpClient(handler));

        var servers = (await service.GetIceServersAsync("user-1")).ToList();

        Assert.NotNull(handler.Request);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://rtc.live.cloudflare.com/v1/turn/keys/key-123/credentials/generate-ice-servers", handler.Request.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal("secret-token", handler.Request.Headers.Authorization.Parameter);
        Assert.Contains("\"ttl\"", handler.Body);

        Assert.Equal(2, servers.Count);
        Assert.Equal(new[] { "stun:stun.cloudflare.com:3478" }, servers[0].Urls);
        Assert.Equal(new[] { "turn:turn.cloudflare.com:3478?transport=udp", "turns:turn.cloudflare.com:443?transport=tcp" }, servers[1].Urls);
        Assert.Equal("cf-user", servers[1].Username);
        Assert.Equal("cf-credential", servers[1].Credential);
    }

    [Fact]
    public async Task Cloudflare_AcceptsLegacySingleObjectResponse()
    {
        var legacy = """{"iceServers":{"urls":["turn:turn.cloudflare.com:3478?transport=udp"],"username":"u","credential":"c"}}""";
        var service = new TurnService(CloudflareOptions(), new HttpClient(new StubHandler(HttpStatusCode.Created, legacy)));

        var server = Assert.Single(await service.GetIceServersAsync("user-1"));
        Assert.Equal("u", server.Username);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Cloudflare_Failure_FallsBackToStun(HttpStatusCode status)
    {
        var service = new TurnService(CloudflareOptions(), new HttpClient(new StubHandler(status, "{}")));

        var server = Assert.Single(await service.GetIceServersAsync("user-1"));
        Assert.Equal(new[] { "stun:stun.cloudflare.com:3478" }, server.Urls);
    }

    [Fact]
    public async Task Cloudflare_WithoutKeys_ThrowsConfigurationError()
    {
        var options = Options.Create(new TurnServerOptions { Provider = "cloudflare" });
        var service = new TurnService(options, new HttpClient(new StubHandler(HttpStatusCode.Created, CloudflareResponse)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetIceServersAsync("user-1"));
    }

    private static IOptions<TurnServerOptions> CloudflareOptions() => Options.Create(new TurnServerOptions
    {
        Provider = "Cloudflare",
        CloudflareKeyId = "key-123",
        CloudflareApiToken = "secret-token"
    });

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
