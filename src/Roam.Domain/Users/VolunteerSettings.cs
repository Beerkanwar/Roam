namespace Roam.Domain.Users;

public class VolunteerSettings
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    
    public bool Enabled { get; set; } = false;
    public bool AvailableForRequests { get; set; } = false;
    
    public bool AcceptsPrivateTours { get; set; } = true;
    public bool AcceptsGroupTours { get; set; } = true;
    
    public int MaxConcurrentViewers { get; set; } = 10;
    
    public string Languages { get; set; } = "en"; // Comma separated for MVP
    
    public bool TipEnabled { get; set; } = false;
    
    // Navigation Property
    public ApplicationUser? User { get; set; }
}
