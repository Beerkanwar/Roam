namespace Roam.Application.Sessions;

public class TurnServerOptions
{
    public const string Position = "TurnServer";

    /// <summary>"coturn" (self-hosted, shared secret) or "cloudflare" (Cloudflare Realtime TURN).</summary>
    public string Provider { get; set; } = "coturn";

    /// <summary>coturn: TURN URIs, e.g. turn:turn.example.com:3478.</summary>
    public string[] Uris { get; set; } = Array.Empty<string>();
    public string Username { get; set; } = string.Empty;

    /// <summary>coturn: the static auth secret (use-auth-secret).</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Cloudflare: the TURN key ID ("Turn Token ID") from the Realtime dashboard.</summary>
    public string CloudflareKeyId { get; set; } = string.Empty;

    /// <summary>Cloudflare: the TURN key API token. Server-side only; never sent to clients.</summary>
    public string CloudflareApiToken { get; set; } = string.Empty;

    /// <summary>STUN servers returned when no TURN relay is configured (peer-to-peer only).</summary>
    public string[] StunUris { get; set; } = { "stun:stun.cloudflare.com:3478" };
}
