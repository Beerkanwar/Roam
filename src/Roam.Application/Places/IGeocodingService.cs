namespace Roam.Application.Places;

public class GeocodingResult
{
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Description { get; set; } = string.Empty;
}

public interface IGeocodingService
{
    Task<IEnumerable<GeocodingResult>> GeocodeAsync(string query, CancellationToken cancellationToken = default);
}
