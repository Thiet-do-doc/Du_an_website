using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.ChatNotifications.DTOs;
using DatingApp.Api.Modules.ChatNotifications.Entities;
using DatingApp.Api.Modules.ChatNotifications.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.Api.Modules.ChatNotifications.Services;

// TV3 calls EnsureConversationAsync INSIDE the same transaction that creates Match.
// PublishMatchAsync must run only AFTER that transaction commits successfully.
public class MatchChatBridge(AppDbContext db, NotificationService notifications,
    IHubContext<ChatHub> hub)
{
    public async Task<Conversation> EnsureConversationAsync(Guid matchId, CancellationToken ct = default)
    {
        var existing = await db.Conversations.SingleOrDefaultAsync(c => c.MatchId == matchId, ct);
        if (existing is not null) return existing;
        var created = new Conversation { MatchId = matchId };
        db.Conversations.Add(created);
        await db.SaveChangesAsync(ct);
        // UNIQUE(MatchId) is the final guard against concurrent duplicates.
        return created;
    }

    // Queue notifications inside the caller's DB transaction, then publish AFTER commit.
    public (AppNotification A, AppNotification B) QueueMatchNotifications(
        Guid matchId, Guid userA, Guid userB) =>
        (notifications.Queue(userA, "match", matchId),
         notifications.Queue(userB, "match", matchId));

    public async Task PublishMatchAsync(Guid matchId, Guid userA, Guid userB,
        AppNotification notificationA, AppNotification notificationB, CancellationToken ct = default)
    {
        await hub.Clients.Users(userA.ToString(), userB.ToString())
            .SendAsync("MatchCreated", new { matchId }, ct);
        await notifications.PublishAsync(notificationA, ct);
        await notifications.PublishAsync(notificationB, ct);
    }
}
