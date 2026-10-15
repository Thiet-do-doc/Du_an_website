namespace DatingApp.Api.Modules.DiscoveryMatching.Entities;

public sealed class Pass
{
    public Guid FromUserId { get; set; }
    public Guid ToUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
