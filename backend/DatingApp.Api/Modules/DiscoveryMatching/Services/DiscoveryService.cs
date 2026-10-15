using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.DiscoveryMatching.DTOs;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.Api.Modules.DiscoveryMatching.Services;

public sealed class DiscoveryService(AppDbContext db, IDiscoveryProfileQuery profiles, IBlockLookup blocks)
{
    public async Task<DiscoveryPage> SearchAsync(Guid viewerId, DiscoveryQuery query, CancellationToken cancellationToken)
    {
        if (viewerId == Guid.Empty) throw new ArgumentException("CurrentUser không hợp lệ.");
        if (query.Page is < 1 or > 100_000) throw new ArgumentOutOfRangeException(nameof(query.Page));
        if (query.PageSize is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(query.PageSize));
        if (query.MinAge is < 18 or > 120 || query.MaxAge is < 18 or > 120 ||
            query.MinAge.HasValue && query.MaxAge.HasValue && query.MinAge > query.MaxAge)
            throw new ArgumentException("Khoảng tuổi không hợp lệ; độ tuổi phải từ 18 trở lên.");

        var blocked = await blocks.GetBlockedUserIdsAsync(viewerId, cancellationToken);
        var excluded = new HashSet<Guid>(blocked) { viewerId };
        var outgoingLikes = await db.UserLikes.AsNoTracking().Where(x => x.FromUserId == viewerId)
            .Select(x => x.ToUserId).ToListAsync(cancellationToken);
        var outgoingPasses = await db.Passes.AsNoTracking().Where(x => x.FromUserId == viewerId)
            .Select(x => x.ToUserId).ToListAsync(cancellationToken);
        var matches = await db.Matches.AsNoTracking().Where(x => x.UnmatchedAt == null && (x.UserLowId == viewerId || x.UserHighId == viewerId))
            .Select(x => x.UserLowId == viewerId ? x.UserHighId : x.UserLowId).ToListAsync(cancellationToken);
        excluded.UnionWith(outgoingLikes);
        excluded.UnionWith(outgoingPasses);
        excluded.UnionWith(matches);

        // Ask for one extra row so HasMore is accurate without an unbounded profile read.
        var pageQuery = query with { PageSize = query.PageSize + 1 };
        var items = await profiles.SearchAsync(viewerId, pageQuery, excluded, cancellationToken);
        var hasMore = items.Count > query.PageSize;
        return new DiscoveryPage(items.Take(query.PageSize).ToArray(), query.Page, query.PageSize, hasMore);
    }
}
