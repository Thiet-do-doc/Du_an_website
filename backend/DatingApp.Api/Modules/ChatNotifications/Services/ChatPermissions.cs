using DatingApp.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.Api.Modules.ChatNotifications.Services;

public record ConversationAccess(bool CanRead, bool CanSend, Guid PartnerId, Guid MatchId)
{
    public static ConversationAccess Denied => new(false, false, Guid.Empty, Guid.Empty);
}

// Integration seam: replace this DEMO implementation with calls to TV1/TV3/TV5's
// real CurrentUser/Match/Block services, while preserving the IChatPermissions contract.
public interface IChatPermissions
{
    Task<ConversationAccess> GetAccessAsync(Guid conversationId, Guid userId, CancellationToken ct = default);
    Task<bool> IsUserActiveAsync(Guid userId, CancellationToken ct = default);
}

public sealed class DemoChatPermissions(AppDbContext db) : IChatPermissions
{
    public async Task<bool> IsUserActiveAsync(Guid userId, CancellationToken ct = default) =>
        await db.DemoUsers.AnyAsync(u => u.Id == userId && u.IsActive && u.IsVerified, ct);

    public async Task<ConversationAccess> GetAccessAsync(
        Guid conversationId, Guid userId, CancellationToken ct = default)
    {
        var m = await (from c in db.Conversations.AsNoTracking()
                       join match in db.DemoMatches.AsNoTracking() on c.MatchId equals match.Id
                       where c.Id == conversationId
                       select new { match.Id, match.UserAId, match.UserBId, match.IsActive })
            .SingleOrDefaultAsync(ct);
        if (m is null || (m.UserAId != userId && m.UserBId != userId))
            return ConversationAccess.Denied;

        var partnerId = m.UserAId == userId ? m.UserBId : m.UserAId;
        var accountAllowed = await db.DemoUsers
            .Where(u => u.Id == userId || u.Id == partnerId)
            .CountAsync(u => u.IsActive && u.IsVerified, ct) == 2;
        if (!accountAllowed) return ConversationAccess.Denied;

        var blocked = await db.DemoBlocks.AnyAsync(b =>
            (b.BlockerId == userId && b.BlockedId == partnerId)
            || (b.BlockerId == partnerId && b.BlockedId == userId), ct);
        if (blocked) return ConversationAccess.Denied;

        // Unmatched chat remains readable but becomes read-only.
        return new ConversationAccess(true, m.IsActive, partnerId, m.Id);
    }
}
