using Microsoft.AspNetCore.Identity;

namespace Roam.Domain.Users;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? ProfilePhotoUrl { get; set; }
    
    public string AccountStatus { get; set; } = "Active";
    public string VolunteerStatus { get; set; } = "None"; // e.g. None, Pending, Approved
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public VolunteerSettings? VolunteerSettings { get; set; }
}
