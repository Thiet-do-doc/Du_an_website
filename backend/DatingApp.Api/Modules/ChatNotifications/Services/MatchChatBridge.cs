using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.ChatNotifications.Entities;
using DatingApp.Api.Modules.ChatNotifications.Hubs;
using DatingApp.Api.Modules.DiscoveryMatching.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.Api.Modules.ChatNotifications.Services;

/// <summary>
/// Implements TV3's contract. EnsureConversationAsync uses the caller's scoped AppDbContext and
/// must be called inside MatchService's current transaction. Publish methods are post-commit only.
/// </summary>
public sealed class MatchChatBridge(AppDbContext db, IChatMatchAccess matches,
    NotificationService notifications, IHubContext<ChatHub> hub) : IMatchConversationBridge
{
    public async Task<Guid> EnsureConversationAsync(Guid matchId, CancellationToken cancellationToken)
    {
        var match = await matches.FindAsync(matchId, cancellationToken)
            ?? throw new ChatDomainException(404, "Không tìm thấy match để tạo hội thoại.");
        if (match.UnmatchedAtUtc is not null)
            throw new ChatDomainException(409, "Không tạo hội thoại mới cho match đã bị hủy.");
        if (match.UserLowId == match.UserHighId || match.UserLowId == Guid.Empty || match.UserHighId == Guid.Empty)
            throw new ChatDomainException(400, "Match có thành viên không hợp lệ.");

        var existing = await db.Conversations.SingleOrDefaultAsync(x => x.MatchId == matchId, cancellationToken);
        if (existing is not null)
        {
            if (existing.UserLowId != match.UserLowId || existing.UserHighId != match.UserHighId)
                throw new InvalidOperationException("Conversation participants do not match the real Match row.");
            return existing.Id;
        }

        var conversation = new Conversation
        {
            MatchId = match.Id,
            UserLowId = match.UserLowId,
            UserHighId = match.UserHighId
        };
        db.Conversations.Add(conversation);
        // SaveChanges joins the transaction opened by MatchService; do not start a nested transaction here.
        await db.SaveChangesAsync(cancellationToken);
        return conversation.Id;
    }

    public async Task<IReadOnlyDictionary<Guid, Guid>> GetConversationIdsAsync(
        IReadOnlyCollection<Guid> matchIds, CancellationToken cancellationToken)
    {
        if (matchIds.Count == 0) return new Dictionary<Guid, Guid>();
        var uniqueIds = matchIds.Distinct().ToArray();
        var rows = await db.Conversations.AsNoTracking()
            .Where(c => uniqueIds.Contains(c.MatchId))
            .Select(c => new { c.MatchId, c.Id }).ToListAsync(cancellationToken);
        var result = rows.ToDictionary(x => x.MatchId, x => x.Id);
        var missing = uniqueIds.Where(id => !result.ContainsKey(id)).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException("Match đang hoạt động chưa có Conversation. Chạy kiểm tra dữ liệu/migration trước khi trả danh sách Match.");
        return result;
    }

    public async Task PublishMatchCreatedAsync(Guid matchId, CancellationToken cancellationToken)
    {
        var match = await matches.FindAsync(matchId, cancellationToken)
            ?? throw new ChatDomainException(404, "Không tìm thấy match vừa tạo.");
        if (match.UnmatchedAtUtc is not null) return;
        var conversationId = await db.Conversations.AsNoTracking()
            .Where(c => c.MatchId == matchId).Select(c => (Guid?)c.Id).SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("PublishMatchCreatedAsync phải chạy sau khi conversation đã commit.");
var noticeLow = await notifications.EnsureMatchAsync(match.UserLowId, matchId, conversationId, cancellationToken);
var noticeHigh = await notifications.EnsureMatchAsync(match.UserHighId, matchId, conversationId, cancellationToken);        await hub.Clients.Users(new[] { match.UserLowId.ToString(), match.UserHighId.ToString() })
            .SendAsync("MatchCreated", new { matchId, conversationId }, cancellationToken);
        await notifications.PublishAsync(noticeLow, cancellationToken);
        await notifications.PublishAsync(noticeHigh, cancellationToken);
    }

    public async Task MatchEndedAsync(Guid matchId, CancellationToken cancellationToken)
    {
        var match = await matches.FindAsync(matchId, cancellationToken);
        if (match is null) return;
        await hub.Clients.Users(new[] { match.UserLowId.ToString(), match.UserHighId.ToString() })
            .SendAsync("MatchEnded", new { matchId }, cancellationToken);
    }
}
