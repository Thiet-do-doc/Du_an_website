using DatingApp.Api.Modules.Profiles.Contracts;

namespace DatingApp.Api.Modules.Profiles.Services;

public interface IDiscoveryProfileQuery
{
    Task<ProfileSummary?> GetSummaryAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> IsCompleteAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProfileSummary>> SearchAsync(Guid viewerId, ProfileSearchCriteria criteria,
        IReadOnlySet<Guid> excludedUserIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProfileSummary>> GetByIdsAsync(Guid viewerId, IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);
}
