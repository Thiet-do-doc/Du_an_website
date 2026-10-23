namespace DatingApp.Api.Modules.DiscoveryMatching.Entities;

using DatingApp.Api.Modules.Auth.Entities;

public sealed class Match
{
    public Guid Id { get; set; }
    public Guid UserLowId { get; set; }
    public Guid UserHighId { get; set; }
    public DateTime MatchedAt { get; set; }
    public DateTime? UnmatchedAt { get; set; }
    public ApplicationUser LowUser { get; set; } = null!;
    public ApplicationUser HighUser { get; set; } = null!;
}
