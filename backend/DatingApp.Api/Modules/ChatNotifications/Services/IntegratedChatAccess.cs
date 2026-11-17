using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.SafetyAdmin.Services;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.Api.Modules.ChatNotifications.Services;

public sealed class IntegratedChatMatchAccess(AppDbContext db) : IChatMatchAccess
{
    public Task<ChatMatchSnapshot?> FindAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        db.Matches.AsNoTracking().Where(x => x.Id == matchId)
            .Select(x => new ChatMatchSnapshot(x.Id, x.UserLowId, x.UserHighId, x.UnmatchedAt))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ChatMatchSnapshot>> ListForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await db.Matches.AsNoTracking().Where(x => x.UserLowId == userId || x.UserHighId == userId)
            .Select(x => new ChatMatchSnapshot(x.Id, x.UserLowId, x.UserHighId, x.UnmatchedAt))
            .ToListAsync(cancellationToken);
}

public sealed class IntegratedChatBlockPolicy(IBlockPolicy blocks) : IChatBlockPolicy
{
    public Task<BlockDirection> GetDirectionAsync(Guid viewerId, Guid partnerId, CancellationToken cancellationToken = default) =>
        blocks.GetDirectionAsync(viewerId, partnerId, cancellationToken);
}
