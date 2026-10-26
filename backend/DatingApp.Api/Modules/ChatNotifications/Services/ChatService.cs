using System.Data;
using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.ChatNotifications.DTOs;
using DatingApp.Api.Modules.ChatNotifications.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DatingApp.Api.Modules.ChatNotifications.Services;

public sealed record SendResult(MessageDto Message, bool IsNew, Guid PartnerId, NotificationDto? Notification);

public sealed class ChatService(AppDbContext db, IChatPermissions permissions,
    IChatMatchAccess matches, NotificationService notifications)
{
    public async Task<ConversationPage> ListConversationsAsync(Guid userId, int page, int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var matchRows = await matches.ListForUserAsync(userId, ct);
        var matchIds = matchRows.Select(x => x.Id).Distinct().ToArray();
        if (matchIds.Length == 0)
            return new ConversationPage(Array.Empty<ConversationDto>(), page, pageSize, false);

        var candidates = await db.Conversations.AsNoTracking()
            .Where(c => matchIds.Contains(c.MatchId) && (c.UserLowId == userId || c.UserHighId == userId))
            .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id).ToListAsync(ct);
        var visible = new List<ConversationDto>();
        foreach (var c in candidates)
        {
            var access = await permissions.GetAccessAsync(c.Id, userId, ct);
            if (!access.CanRead) continue;
            var last = await db.Messages.AsNoTracking().Where(x => x.ConversationId == c.Id)
                .OrderByDescending(x => x.SentAt).ThenByDescending(x => x.Id)
                .Select(x => MessageDto.From(x)).FirstOrDefaultAsync(ct);
            // TV2's ProfileSummary is not on main yet. Do not expose email as a display name.
            visible.Add(new ConversationDto(c.Id, c.MatchId, access.PartnerId,
                "Người dùng", c.CreatedAt, last, access.CanSend));
        }
        var items = visible.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        return new ConversationPage(items, page, pageSize, visible.Count > page * pageSize);
    }

    public async Task<MessagesPage> GetMessagesAsync(Guid conversationId, Guid userId,
        int limit, string? cursor, Guid? afterId, CancellationToken ct = default)
    {
        var access = await permissions.GetAccessAsync(conversationId, userId, ct);
        if (!access.CanRead)
            throw new ChatDomainException(404, "Không tìm thấy hội thoại hoặc không có quyền.");
        if (cursor is not null && afterId is not null)
            throw new ChatDomainException(400, "Không dùng đồng thời cursor và afterId.");
        limit = Math.Clamp(limit, 1, 50);

        IQueryable<ChatMessage> query = db.Messages.AsNoTracking()
            .Where(x => x.ConversationId == conversationId);
        if (afterId is not null)
        {
            var anchor = await query.SingleOrDefaultAsync(x => x.Id == afterId.Value, ct);
            if (anchor is null) throw new ChatDomainException(400, "afterId không thuộc hội thoại.");
            query = db.Messages.FromSqlInterpolated(
                $"SELECT * FROM \"Messages\" WHERE \"ConversationId\" = {conversationId} AND (\"SentAt\", \"Id\") > ({anchor.SentAt}, {anchor.Id})")
                .AsNoTracking();
            var newer = await query.OrderBy(x => x.SentAt).ThenBy(x => x.Id)
                .Take(limit + 1).ToListAsync(ct);
            var hasMore = newer.Count > limit;
            var items = newer.Take(limit).ToList();
            return new MessagesPage(items.Select(MessageDto.From).ToArray(), null,
                hasMore && items.Count > 0 ? items[^1].Id : null);
        }

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            if (!CursorCodec.TryDecode(cursor, out var sentAt, out var id))
                throw new ChatDomainException(400, "Cursor tin nhắn không hợp lệ.");
            query = db.Messages.FromSqlInterpolated(
                $"SELECT * FROM \"Messages\" WHERE \"ConversationId\" = {conversationId} AND (\"SentAt\", \"Id\") < ({sentAt}, {id})")
                .AsNoTracking();
        }

        var older = await query.OrderByDescending(x => x.SentAt).ThenByDescending(x => x.Id)
            .Take(limit + 1).ToListAsync(ct);
        var more = older.Count > limit;
        var pageItems = older.Take(limit).ToList();
        var next = more && pageItems.Count > 0
            ? CursorCodec.Encode(pageItems[^1].SentAt, pageItems[^1].Id) : null;
        pageItems.Reverse();
        return new MessagesPage(pageItems.Select(MessageDto.From).ToArray(), next, null);
    }

    public async Task<SendResult> SendMessageAsync(Guid conversationId, Guid senderId,
        string? content, Guid clientMessageId, CancellationToken ct = default)
    {
        content = MessageRules.Validate(content, clientMessageId);
        var access = await permissions.GetAccessAsync(conversationId, senderId, ct);
        if (!access.CanSend)
            throw new ChatDomainException(403, "Không có quyền gửi tin trong hội thoại.");

        // The unique index is the last guard; retrying the same sender/clientMessageId is idempotent.
        var existing = await db.Messages.AsNoTracking()
            .SingleOrDefaultAsync(x => x.SenderId == senderId && x.ClientMessageId == clientMessageId, ct);
        if (existing is not null)
        {
            EnsureSamePayload(existing, conversationId, content);
            var oldNotification = await notifications.EnsureNewMessageAsync(
                access.PartnerId, existing.Id, conversationId, ct);
            return new SendResult(MessageDto.From(existing), false, access.PartnerId,
                NotificationDto.From(oldNotification));
        }

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            // Re-check match, block, and account status in the transaction immediately before write.
            access = await permissions.GetAccessAsync(conversationId, senderId, ct);
            if (!access.CanSend)
                throw new ChatDomainException(403, "Quyền gửi tin đã thay đổi.");

            var message = new ChatMessage
            {
                ConversationId = conversationId,
                SenderId = senderId,
                ClientMessageId = clientMessageId,
                Content = content
            };
            db.Messages.Add(message);
            var notice = notifications.QueueNewMessage(access.PartnerId, message.Id, conversationId);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct); // persistence happens before any realtime event
            return new SendResult(MessageDto.From(message), true, access.PartnerId,
                NotificationDto.From(notice));
        }
        catch (Exception ex) when (FindPostgresState(ex) == PostgresErrorCodes.UniqueViolation)
        {
            await SafeRollbackAsync(tx, ct);
            db.ChangeTracker.Clear();
            var duplicate = await db.Messages.AsNoTracking().SingleOrDefaultAsync(x =>
                x.SenderId == senderId && x.ClientMessageId == clientMessageId, ct);
            if (duplicate is null) throw new ChatDomainException(409, "Xung đột dữ liệu; hãy thử lại.");
            EnsureSamePayload(duplicate, conversationId, content);
            var notice = await notifications.EnsureNewMessageAsync(access.PartnerId, duplicate.Id, conversationId, ct);
            return new SendResult(MessageDto.From(duplicate), false, access.PartnerId,
                NotificationDto.From(notice));
        }
        catch (Exception ex) when (FindPostgresState(ex) == PostgresErrorCodes.SerializationFailure)
        {
            await SafeRollbackAsync(tx, ct);
            db.ChangeTracker.Clear();
            throw new ChatDomainException(409, "Có thay đổi đồng thời; vui lòng thử gửi lại cùng clientMessageId.");
        }
    }

    private static void EnsureSamePayload(ChatMessage message, Guid conversationId, string content)
    {
        if (message.ConversationId != conversationId || !string.Equals(message.Content, content, StringComparison.Ordinal))
            throw new ChatDomainException(409, "clientMessageId đã được dùng với hội thoại hoặc nội dung khác.");
    }

    private static string? FindPostgresState(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is PostgresException postgres) return postgres.SqlState;
        return null;
    }

    private static async Task SafeRollbackAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx,
        CancellationToken ct)
    {
        try { await tx.RollbackAsync(ct); }
        catch { /* Preserve the original persistence error. */ }
    }
}
