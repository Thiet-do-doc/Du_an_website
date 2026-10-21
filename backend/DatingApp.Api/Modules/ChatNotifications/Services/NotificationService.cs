using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.ChatNotifications.DTOs;
using DatingApp.Api.Modules.ChatNotifications.Entities;
using DatingApp.Api.Modules.ChatNotifications.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DatingApp.Api.Modules.ChatNotifications.Services;

public sealed class NotificationService(AppDbContext db, IHubContext<ChatHub> hub)
{
    /// <summary>Queue inside the message transaction. The caller persists it with the message.</summary>
    public AppNotification QueueNewMessage(Guid recipientId, Guid messageId, Guid conversationId)
    {
        var n = new AppNotification
        {
            RecipientId = recipientId,
            ConversationId = conversationId,
            Type = "newMessage",
            TargetId = messageId,
            DedupeKey = $"message:{messageId:N}"
        };
        db.Notifications.Add(n);
        return n;
    }

    /// <summary>
    /// Idempotently creates a notification for a message that was already committed, e.g. when a
    /// client retries the same clientMessageId after the ACK was lost.
    /// </summary>
    public Task<AppNotification> EnsureNewMessageAsync(Guid recipientId, Guid messageId,
        Guid conversationId, CancellationToken cancellationToken = default) =>
        GetOrCreateAsync(recipientId, "newMessage", messageId, conversationId,
            $"message:{messageId:N}", cancellationToken);

    public Task<AppNotification> EnsureMatchAsync(Guid recipientId, Guid matchId,
        Guid conversationId, CancellationToken cancellationToken = default) =>
        GetOrCreateAsync(recipientId, "match", matchId, conversationId,
            $"match:{matchId:N}", cancellationToken);

    private async Task<AppNotification> GetOrCreateAsync(Guid recipientId, string type, Guid targetId,
        Guid conversationId, string dedupeKey, CancellationToken ct)
    {
        var existing = await db.Notifications.AsNoTracking()
            .SingleOrDefaultAsync(x => x.RecipientId == recipientId && x.DedupeKey == dedupeKey, ct);
        if (existing is not null) return existing;

        var created = new AppNotification
        {
            RecipientId = recipientId,
            Type = type,
            TargetId = targetId,
            ConversationId = conversationId,
            DedupeKey = dedupeKey
        };
        db.Notifications.Add(created);
        try
        {
            await db.SaveChangesAsync(ct);
            return created;
        }
        catch (DbUpdateException ex) when (FindPostgresState(ex) == PostgresErrorCodes.UniqueViolation)
        {
            // Another retry/publisher won the unique (RecipientId, DedupeKey) key.
            db.ChangeTracker.Clear();
            return await db.Notifications.AsNoTracking()
                .SingleAsync(x => x.RecipientId == recipientId && x.DedupeKey == dedupeKey, ct);
        }
    }

    public async Task PublishAsync(AppNotification notification, CancellationToken ct = default) =>
        await hub.Clients.User(notification.RecipientId.ToString())
            .SendAsync("NotificationCreated", NotificationDto.From(notification), ct);

    public async Task<NotificationsPage> ListAsync(Guid userId, int page, int pageSize,
        string? cursor = null, Guid? afterId = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        if (cursor is not null && afterId is not null)
            throw new ChatDomainException(400, "Không dùng đồng thời cursor và afterId.");

        IQueryable<AppNotification> query = db.Notifications.AsNoTracking()
            .Where(x => x.RecipientId == userId);

        if (afterId is not null)
        {
            var anchor = await query.SingleOrDefaultAsync(x => x.Id == afterId.Value, ct);
            if (anchor is null) throw new ChatDomainException(400, "afterId không thuộc người nhận.");
            query = db.Notifications.FromSqlInterpolated(
                $"SELECT * FROM \"Notifications\" WHERE \"RecipientId\" = {userId} AND (\"CreatedAt\", \"Id\") > ({anchor.CreatedAt}, {anchor.Id})")
                .AsNoTracking();
            var newer = await query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
                .Take(pageSize + 1).ToListAsync(ct);
            var hasNewer = newer.Count > pageSize;
            var items = newer.Take(pageSize).ToList();
            return new NotificationsPage(items.Select(NotificationDto.From).ToArray(), page, pageSize,
                hasNewer, null, hasNewer && items.Count > 0 ? items[^1].Id : null);
        }

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            if (!CursorCodec.TryDecode(cursor, out var createdAt, out var id))
                throw new ChatDomainException(400, "Cursor thông báo không hợp lệ.");
            query = db.Notifications.FromSqlInterpolated(
                $"SELECT * FROM \"Notifications\" WHERE \"RecipientId\" = {userId} AND (\"CreatedAt\", \"Id\") < ({createdAt}, {id})")
                .AsNoTracking();
        }

        var older = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Take(pageSize + 1).ToListAsync(ct);
        var hasMore = older.Count > pageSize;
        var pageItems = older.Take(pageSize).ToList();
        var nextCursor = hasMore && pageItems.Count > 0
            ? CursorCodec.Encode(pageItems[^1].CreatedAt, pageItems[^1].Id) : null;
        return new NotificationsPage(pageItems.Select(NotificationDto.From).ToArray(), page, pageSize,
            hasMore, nextCursor, null);
    }

    public async Task<NotificationDto> MarkReadAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var n = await db.Notifications.SingleOrDefaultAsync(x => x.Id == id && x.RecipientId == userId, ct);
        if (n is null) throw new ChatDomainException(404, "Không tìm thấy thông báo hoặc không có quyền.");
        n.ReadAt ??= DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return NotificationDto.From(n);
    }

    private static string? FindPostgresState(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
            if (current is PostgresException postgres) return postgres.SqlState;
        return null;
    }
}
