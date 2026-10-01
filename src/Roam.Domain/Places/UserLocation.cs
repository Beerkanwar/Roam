using NetTopologySuite.Geometries;

namespace Roam.Domain.Places;

public class UserLocation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    
    // The approximate location of the user
    public Point Location { get; set; } = Point.Empty;
    
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}
