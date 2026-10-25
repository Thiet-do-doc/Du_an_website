using DiscoveryProfileSummary = DatingApp.Api.Modules.DiscoveryMatching.DTOs.ProfileSummary;
using DatingApp.Api.Modules.DiscoveryMatching.DTOs;
using DatingApp.Api.Modules.Profiles.Contracts;

namespace DatingApp.Api.Modules.DiscoveryMatching.Services;

/// <summary>Maps the Profiles-owned query contract to DiscoveryMatching's public projection.</summary>
public sealed class DiscoveryProfileQueryAdapter(
    DatingApp.Api.Modules.Profiles.Services.IDiscoveryProfileQuery profiles) : IDiscoveryProfileQuery
{
    public Task<bool> IsCompleteAsync(Guid userId, CancellationToken cancellationToken) =>
        profiles.IsCompleteAsync(userId, cancellationToken);

    public async Task<IReadOnlyList<DiscoveryProfileSummary>> SearchAsync(Guid viewerId, DiscoveryQuery query,
        IReadOnlySet<Guid> excludedUserIds, CancellationToken cancellationToken)
    {
        var criteria = new ProfileSearchCriteria(query.Page, query.PageSize, query.MinAge, query.MaxAge, query.City, query.Interest);
        var items = await profiles.SearchAsync(viewerId, criteria, excludedUserIds, cancellationToken);
        return items.Select(Map).ToArray();
    }

    public async Task<IReadOnlyList<DiscoveryProfileSummary>> GetByIdsAsync(Guid viewerId,
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        var items = await profiles.GetByIdsAsync(viewerId, userIds, cancellationToken);
        return items.Select(Map).ToArray();
    }

    private static DiscoveryProfileSummary Map(DiscoveryProfileSummary profile) => profile;
}
