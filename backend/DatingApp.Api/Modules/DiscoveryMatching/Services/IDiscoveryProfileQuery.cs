using DatingApp.Api.Modules.DiscoveryMatching.DTOs;

namespace DatingApp.Api.Modules.DiscoveryMatching.Services;

/// <summary>TV2 adapter. Must exclude hidden/locked/incomplete profiles and filter by age without returning DOB.</summary>
public interface IDiscoveryProfileQuery
{
    Task<bool> IsCompleteAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProfileSummary>> SearchAsync(
        Guid viewerId, DiscoveryQuery query, IReadOnlySet<Guid> excludedUserIds, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProfileSummary>> GetByIdsAsync(
        Guid viewerId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}
