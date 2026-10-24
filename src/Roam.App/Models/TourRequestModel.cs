namespace Roam.App.Models;

public class TourRequestModel
{
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Mode { get; set; } // 0 = Instant, 1 = Scheduled
    public int Visibility { get; set; } // 0 = Private, 1 = Group
    public PlaceModel? Place { get; set; }

    public string ModeDisplay => Mode == 0 ? "⚡ Instant Live" : "📅 Scheduled";
    public string VisibilityDisplay => Visibility == 0 ? "🔒 Private (1-on-1)" : "👥 Public Group";
    public string PlaceName => Place?.Name ?? "Custom Coordinates";
    public string DescriptionSnippet => string.IsNullOrWhiteSpace(Description) 
        ? "No specific instructions provided." 
        : (Description.Length > 120 ? Description[..117] + "..." : Description);
    public string LocationDisplay => Place != null 
        ? $"{Place.Latitude:F4}, {Place.Longitude:F4}" 
        : "GPS Coordinates";
}
