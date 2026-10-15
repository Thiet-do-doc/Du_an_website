namespace DatingApp.Api.Modules.DiscoveryMatching.Entities;

public sealed class Match
{
    public Guid Id { get; set; }
    public Guid UserLowId { get; set; }
    public Guid UserHighId { get; set; }
    public DateTime MatchedAt { get; set; }
    public DateTime? UnmatchedAt { get; set; }
}
