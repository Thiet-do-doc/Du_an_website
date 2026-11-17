using DatingApp.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.Api.Modules.SafetyAdmin.Services;

[Flags]
public enum BlockDirection { None = 0, Outgoing = 1, Incoming = 2 }

public interface IBlockPolicy
{
    Task<bool> CanInteractAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken = default);
    Task<BlockDirection> GetDirectionAsync(Guid viewerId, Guid partnerId, CancellationToken cancellationToken = default);
}

public sealed class BlockPolicy(AppDbContext db) : IBlockPolicy
{
    public async Task<bool> CanInteractAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken = default)
    {
        if (firstUserId == Guid.Empty || secondUserId == Guid.Empty) return false;
        if (firstUserId == secondUserId) return true;
        return await GetDirectionAsync(firstUserId, secondUserId, cancellationToken) == BlockDirection.None;
    }

    public async Task<BlockDirection> GetDirectionAsync(Guid viewerId, Guid partnerId, CancellationToken cancellationToken = default)
    {
        var blockers = await db.Blocks.AsNoTracking()
            .Where(x => (x.BlockerUserId == viewerId && x.BlockedUserId == partnerId) ||
                (x.BlockerUserId == partnerId && x.BlockedUserId == viewerId))
            .Select(x => x.BlockerUserId)
            .ToListAsync(cancellationToken);
        var outgoing = blockers.Contains(viewerId) ? BlockDirection.Outgoing : BlockDirection.None;
        var incoming = blockers.Contains(partnerId) ? BlockDirection.Incoming : BlockDirection.None;
        return outgoing | incoming;
    }
}
