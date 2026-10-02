using Microsoft.AspNetCore.Identity;

namespace Roam.Domain.Users;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? ProfilePhotoUrl { get; set; }
    
    public AccountStatus AccountStatus { get; set; } = AccountStatus.Active;
    public VolunteerStatus VolunteerStatus { get; set; } = VolunteerStatus.None;
    
    public DateOnly? DateOfBirth { get; set; }
    public AgeCategory AgeCategory { get; set; } = AgeCategory.Unknown;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public VolunteerSettings? VolunteerSettings { get; set; }
}
