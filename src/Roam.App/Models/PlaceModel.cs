namespace Roam.App.Models;

public class PlaceModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public string CoordinatesDisplay => $"{Latitude:F4}, {Longitude:F4}";
}
