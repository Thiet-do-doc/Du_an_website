using Microsoft.AspNetCore.Identity;

namespace DatingApp.Api.Modules.Auth.Entities;

public sealed class ApplicationUser : IdentityUser
{
    public DateOnly DateOfBirth { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
