namespace Roam.Application.Sessions;

public class TurnServerOptions
{
    public const string Position = "TurnServer";

    public string[] Uris { get; set; } = Array.Empty<string>();
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
