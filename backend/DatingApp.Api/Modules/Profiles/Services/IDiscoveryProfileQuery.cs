using DatingApp.Api.Modules.DiscoveryMatching.DTOs;

namespace DatingApp.Api.Modules.Profiles.Services;

public interface IDiscoveryProfileQuery
{
    Task<ProfileSummary?> GetSummaryAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> IsCompleteAsync(Guid userId, CancellationToken cancellationToken = default);
}
