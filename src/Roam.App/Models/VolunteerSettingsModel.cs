namespace Roam.App.Models;

public class VolunteerSettingsModel
{
    public bool Enabled { get; set; } = true;
    public bool AvailableForRequests { get; set; } = true;
    public bool AcceptsPrivateTours { get; set; } = true;
    public bool AcceptsGroupTours { get; set; } = true;
    public int MaxConcurrentViewers { get; set; } = 10;
    public string Languages { get; set; } = "English";
    public bool TipEnabled { get; set; } = false;
}
