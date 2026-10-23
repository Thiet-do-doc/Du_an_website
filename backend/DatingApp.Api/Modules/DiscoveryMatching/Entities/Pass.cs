namespace DatingApp.Api.Modules.DiscoveryMatching.Entities;

using DatingApp.Api.Modules.Auth.Entities;

public sealed class Pass
{
    public Guid FromUserId { get; set; }
    public Guid ToUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public ApplicationUser FromUser { get; set; } = null!;
    public ApplicationUser ToUser { get; set; } = null!;
}
