using DatingApp.Api.Modules.Profiles.Contracts;

namespace DatingApp.Api.Modules.Profiles.Services;

public interface IDiscoveryProfileQuery
{
    Task<ProfileSummary?> GetSummaryAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> IsCompleteAsync(Guid userId, CancellationToken cancellationToken = default);
}
