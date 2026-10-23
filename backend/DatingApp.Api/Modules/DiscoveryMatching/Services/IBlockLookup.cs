namespace DatingApp.Api.Modules.DiscoveryMatching.Services;

/// <summary>TV5 adapter; returns users blocked in either direction from the viewer.</summary>
public interface IBlockLookup
{
    Task<IReadOnlySet<Guid>> GetBlockedUserIdsAsync(Guid viewerId, CancellationToken cancellationToken);
}
