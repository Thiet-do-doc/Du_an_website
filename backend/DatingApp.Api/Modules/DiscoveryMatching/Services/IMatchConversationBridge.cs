namespace DatingApp.Api.Modules.DiscoveryMatching.Services;

/// <summary>Implemented by ChatNotifications (TV4). Must share the scoped AppDbContext and current transaction.</summary>
public interface IMatchConversationBridge
{
    Task<Guid> EnsureConversationAsync(Guid matchId, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, Guid>> GetConversationIdsAsync(IReadOnlyCollection<Guid> matchIds, CancellationToken cancellationToken);
    Task PublishMatchCreatedAsync(Guid matchId, CancellationToken cancellationToken);
    Task MatchEndedAsync(Guid matchId, CancellationToken cancellationToken);
}
