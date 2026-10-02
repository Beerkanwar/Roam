namespace Roam.Application.Sessions;

public class IceServerDto
{
    public string[] Urls { get; set; } = Array.Empty<string>();
    public string Username { get; set; } = string.Empty;
    public string Credential { get; set; } = string.Empty;
}
