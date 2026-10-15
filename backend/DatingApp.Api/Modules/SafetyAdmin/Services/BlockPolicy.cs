using DatingApp.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.Api.Modules.SafetyAdmin.Services;

public interface IBlockPolicy
{
    Task<bool> CanInteractAsync(string firstUserId, string secondUserId, CancellationToken cancellationToken = default);
}

public sealed class BlockPolicy(AppDbContext db) : IBlockPolicy
{
    public async Task<bool> CanInteractAsync(string firstUserId, string secondUserId, CancellationToken cancellationToken = default)
    {
        if (firstUserId == secondUserId) return true;
        var blocked = await db.Blocks.AnyAsync(x =>
            (x.BlockerUserId == firstUserId && x.BlockedUserId == secondUserId) ||
            (x.BlockerUserId == secondUserId && x.BlockedUserId == firstUserId), cancellationToken);
        return !blocked;
    }
}
