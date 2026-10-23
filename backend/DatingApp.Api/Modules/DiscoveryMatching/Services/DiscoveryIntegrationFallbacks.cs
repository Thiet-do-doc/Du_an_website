using DatingApp.Api.Modules.DiscoveryMatching.DTOs;

namespace DatingApp.Api.Modules.DiscoveryMatching.Services;

public sealed class DiscoveryDependencyUnavailableException(string dependency)
    : Exception($"Module DiscoveryMatching đang chờ tích hợp {dependency}.");

// Fail closed until the owning module registers its implementation in DI.
public sealed class UnconfiguredDiscoveryProfileQuery : IDiscoveryProfileQuery
{
    public Task<bool> IsCompleteAsync(Guid userId, CancellationToken cancellationToken) =>
        throw new DiscoveryDependencyUnavailableException("Profiles/TV2");

    public Task<IReadOnlyList<ProfileSummary>> SearchAsync(Guid viewerId, DiscoveryQuery query, IReadOnlySet<Guid> excludedUserIds, CancellationToken cancellationToken) =>
        throw new DiscoveryDependencyUnavailableException("Profiles/TV2");

    public Task<IReadOnlyList<ProfileSummary>> GetByIdsAsync(Guid viewerId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        throw new DiscoveryDependencyUnavailableException("Profiles/TV2");
}

public sealed class UnconfiguredBlockLookup : IBlockLookup
{
    public Task<IReadOnlySet<Guid>> GetBlockedUserIdsAsync(Guid viewerId, CancellationToken cancellationToken) =>
        throw new DiscoveryDependencyUnavailableException("SafetyAdmin/TV5");
}

public sealed class UnconfiguredMatchConversationBridge : IMatchConversationBridge
{
    public Task<Guid> EnsureConversationAsync(Guid matchId, CancellationToken cancellationToken) =>
        throw new DiscoveryDependencyUnavailableException("ChatNotifications/TV4");

    public Task<IReadOnlyDictionary<Guid, Guid>> GetConversationIdsAsync(IReadOnlyCollection<Guid> matchIds, CancellationToken cancellationToken) =>
        throw new DiscoveryDependencyUnavailableException("ChatNotifications/TV4");

    public Task PublishMatchCreatedAsync(Guid matchId, CancellationToken cancellationToken) =>
        throw new DiscoveryDependencyUnavailableException("ChatNotifications/TV4");

    public Task MatchEndedAsync(Guid matchId, CancellationToken cancellationToken) =>
        throw new DiscoveryDependencyUnavailableException("ChatNotifications/TV4");
}
