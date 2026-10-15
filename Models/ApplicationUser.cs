using Microsoft.AspNetCore.Identity;

namespace Vo_T1_2_AuthStarter.Models;

public sealed class ApplicationUser : IdentityUser
{
    public DateOnly DateOfBirth { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
