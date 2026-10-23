using DatingApp.Api.Modules.DiscoveryMatching.DTOs;

namespace DatingApp.Api.Modules.DiscoveryMatching.Services;

public interface IMatchService
{
    Task<LikeResponse> LikeAsync(Guid fromUserId, Guid toUserId, CancellationToken cancellationToken);
    Task PassAsync(Guid fromUserId, Guid toUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MatchResponse>> GetMatchesAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> UnmatchAsync(Guid userId, Guid matchId, CancellationToken cancellationToken);
}
