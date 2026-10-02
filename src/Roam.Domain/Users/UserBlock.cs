using System;

namespace Roam.Domain.Users;

public class UserBlock
{
    public string BlockerId { get; set; } = string.Empty;
    public string BlockedId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
