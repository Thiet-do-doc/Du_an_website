using DatingApp.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.Api.Modules.SafetyAdmin.Services;

public interface IBlockPolicy
{
    Task<bool> CanInteractAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken = default);
}

public sealed class BlockPolicy(AppDbContext db) : IBlockPolicy
{
    public async Task<bool> CanInteractAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken = default)
    {
        if (firstUserId == Guid.Empty || secondUserId == Guid.Empty) return false;
        if (firstUserId == secondUserId) return true;
        var blocked = await db.Blocks.AnyAsync(x =>
            (x.BlockerUserId == firstUserId && x.BlockedUserId == secondUserId) ||
            (x.BlockerUserId == secondUserId && x.BlockedUserId == firstUserId), cancellationToken);
        return !blocked;
    }
}
