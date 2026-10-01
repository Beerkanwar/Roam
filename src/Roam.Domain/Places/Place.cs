using NetTopologySuite.Geometries;

namespace Roam.Domain.Places;

public class Place
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // Physical coordinate of the place
    public Point Location { get; set; } = Point.Empty;
    
    // Administrative boundary or specific attraction boundary
    public Polygon? Boundary { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
