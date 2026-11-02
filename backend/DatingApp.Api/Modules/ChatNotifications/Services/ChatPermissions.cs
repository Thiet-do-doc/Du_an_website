using DatingApp.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.Api.Modules.ChatNotifications.Services;

public sealed record ConversationAccess(bool CanRead, bool CanSend, Guid PartnerId, Guid MatchId)
{
    public static ConversationAccess Denied { get; } = new(false, false, Guid.Empty, Guid.Empty);
}

/// <summary>
/// Adapter contract owned by TV3's real Matches module. It must return real Match rows, including
/// unmatched rows for read-only history. It must share the scoped AppDbContext/transaction.
/// </summary>
public interface IChatMatchAccess
{
    Task<ChatMatchSnapshot?> FindAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatMatchSnapshot>> ListForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed record ChatMatchSnapshot(Guid Id, Guid UserLowId, Guid UserHighId, DateTime? UnmatchedAtUtc);

/// <summary>Adapter contract owned by TV5. Must check blocks in both directions.</summary>
public interface IChatBlockPolicy
{
    Task<bool> CanInteractAsync(Guid firstUserId, Guid secondUserId,
        CancellationToken cancellationToken = default);
}

public sealed class UnconfiguredChatMatchAccess : IChatMatchAccess
{
    public Task<ChatMatchSnapshot?> FindAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        throw new ChatDependencyUnavailableException("DiscoveryMatching/TV3: IChatMatchAccess");

    public Task<IReadOnlyList<ChatMatchSnapshot>> ListForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        throw new ChatDependencyUnavailableException("DiscoveryMatching/TV3: IChatMatchAccess");
}

public sealed class UnconfiguredChatBlockPolicy : IChatBlockPolicy
{
    public Task<bool> CanInteractAsync(Guid firstUserId, Guid secondUserId,
        CancellationToken cancellationToken = default) =>
        throw new ChatDependencyUnavailableException("SafetyAdmin/TV5: IChatBlockPolicy (Guid)");
}

public interface IChatPermissions
{
    Task<ConversationAccess> GetAccessAsync(Guid conversationId, Guid userId,
        CancellationToken cancellationToken = default);
    Task<bool> IsUserActiveAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Real Auth user checks + TV3 Match and TV5 block adapters. Missing dependencies fail closed with 503;
/// no DemoUser/DemoMatch/DemoBlock data is consulted by this service.
/// </summary>
public sealed class ChatPermissions(AppDbContext db, IChatMatchAccess matches, IChatBlockPolicy blocks)
    : IChatPermissions
{
    public Task<bool> IsUserActiveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return db.Users.AsNoTracking().AnyAsync(x => x.Id == userId && x.IsActive && x.EmailConfirmed &&
            (x.LockoutEnd == null || x.LockoutEnd <= now), cancellationToken);
    }

    public async Task<ConversationAccess> GetAccessAsync(Guid conversationId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await db.Conversations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == conversationId, cancellationToken);
        if (conversation is null || (conversation.UserLowId != userId && conversation.UserHighId != userId))
            return ConversationAccess.Denied;

        var match = await matches.FindAsync(conversation.MatchId, cancellationToken);
        if (match is null || match.Id != conversation.MatchId || match.UserLowId == match.UserHighId)
            return ConversationAccess.Denied;
        var matchLow = Min(match.UserLowId, match.UserHighId);
        var matchHigh = Max(match.UserLowId, match.UserHighId);
        if (conversation.UserLowId != matchLow || conversation.UserHighId != matchHigh)
            return ConversationAccess.Denied;

        var now = DateTimeOffset.UtcNow;
        var participantsActive = await db.Users.AsNoTracking()
            .Where(x => (x.Id == matchLow || x.Id == matchHigh) && x.IsActive && x.EmailConfirmed &&
                (x.LockoutEnd == null || x.LockoutEnd <= now))
            .CountAsync(cancellationToken) == 2;
        var canInteract = await blocks.CanInteractAsync(matchLow, matchHigh, cancellationToken);
        return ChatAccessRules.Evaluate(conversation.Id, userId, conversation.UserLowId,
            conversation.UserHighId, match, participantsActive, canInteract);
    }

    private static Guid Min(Guid a, Guid b) => a.CompareTo(b) < 0 ? a : b;
    private static Guid Max(Guid a, Guid b) => a.CompareTo(b) > 0 ? a : b;
}

/// <summary>Pure authorization rule so matched/unmatched/block cases can be unit-tested without fake DB data.</summary>
public static class ChatAccessRules
{
    public static ConversationAccess Evaluate(Guid conversationId, Guid userId,
        Guid conversationLowId, Guid conversationHighId, ChatMatchSnapshot? match,
        bool bothAccountsActive, bool canInteract)
    {
        if (conversationId == Guid.Empty || userId == Guid.Empty || match is null || match.Id == Guid.Empty)
            return ConversationAccess.Denied;
        if (match.UserLowId == Guid.Empty || match.UserHighId == Guid.Empty || match.UserLowId == match.UserHighId)
            return ConversationAccess.Denied;
        var low = match.UserLowId.CompareTo(match.UserHighId) < 0 ? match.UserLowId : match.UserHighId;
        var high = low == match.UserLowId ? match.UserHighId : match.UserLowId;
        if (conversationLowId != low || conversationHighId != high) return ConversationAccess.Denied;
        if (userId != low && userId != high) return ConversationAccess.Denied;
        if (!bothAccountsActive || !canInteract) return ConversationAccess.Denied;
        var partnerId = userId == low ? high : low;
        return new ConversationAccess(true, match.UnmatchedAtUtc is null, partnerId, match.Id);
    }
}
