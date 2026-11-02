using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.DiscoveryMatching.Services;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.Api.Modules.SafetyAdmin.Services;

public sealed class DiscoveryBlockLookup(AppDbContext db) : IBlockLookup
{
    public async Task<IReadOnlySet<Guid>> GetBlockedUserIdsAsync(Guid viewerId, CancellationToken cancellationToken) =>
        (await db.Blocks.AsNoTracking()
            .Where(x => x.BlockerUserId == viewerId || x.BlockedUserId == viewerId)
            .Select(x => x.BlockerUserId == viewerId ? x.BlockedUserId : x.BlockerUserId)
            .ToListAsync(cancellationToken)).ToHashSet();
}
