using System.Data;
using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.ChatNotifications.DTOs;
using DatingApp.Api.Modules.ChatNotifications.Entities;
using DatingApp.Api.Demo;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DatingApp.Api.Modules.ChatNotifications.Services;

public record SendResult(MessageDto Message, bool IsNew, Guid PartnerId, NotificationDto? Notification);

public class ChatService(AppDbContext db, IChatPermissions permissions, NotificationService notifications)
{
    public async Task<ConversationPage> ListConversationsAsync(Guid userId, int page, int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        // Demo relationship query. Replace with TV3's match contract at integration.
        var candidates = await (from c in db.Conversations.AsNoTracking()
                                join m in db.DemoMatches.AsNoTracking() on c.MatchId equals m.Id
                                where m.UserAId == userId || m.UserBId == userId
                                orderby c.CreatedAt descending
                                select c).ToListAsync(ct);
        var visible = new List<ConversationDto>();
        foreach (var c in candidates)
        {
            var access = await permissions.GetAccessAsync(c.Id, userId, ct);
            if (!access.CanRead) continue;
            var last = await db.Messages.AsNoTracking().Where(x => x.ConversationId == c.Id)
                .OrderByDescending(x => x.SentAt).ThenByDescending(x => x.Id)
                .Select(x => MessageDto.From(x)).FirstOrDefaultAsync(ct);
            var partnerName = await db.DemoUsers.Where(u => u.Id == access.PartnerId)
                .Select(u => u.DisplayName).SingleAsync(ct);
            visible.Add(new ConversationDto(c.Id, c.MatchId, access.PartnerId,
                partnerName, c.CreatedAt, last, access.CanSend));
        }
        var items = visible.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new ConversationPage(items, page, pageSize,
            visible.Count > page * pageSize);
    }

    public async Task<MessagesPage> GetMessagesAsync(Guid conversationId, Guid userId,
        int limit, string? cursor, Guid? afterId, CancellationToken ct = default)
    {
        var access = await permissions.GetAccessAsync(conversationId, userId, ct);
        if (!access.CanRead) throw new ChatDomainException(404, "Không tìm thấy hội thoại hoặc không có quyền.");
        if (cursor is not null && afterId is not null)
            throw new ChatDomainException(400, "Không dùng đồng thời cursor và afterId.");
        limit = Math.Clamp(limit, 1, 50);

        IQueryable<ChatMessage> query = db.Messages.AsNoTracking()
            .Where(x => x.ConversationId == conversationId);
        if (afterId is not null)
        {
            var anchor = await query.SingleOrDefaultAsync(x => x.Id == afterId.Value, ct);
            if (anchor is null) throw new ChatDomainException(400, "afterId không thuộc hội thoại.");
            // PostgreSQL compares both keyset columns as a tuple: stable under ties.
            query = db.Messages.FromSqlInterpolated(
                $"SELECT * FROM \"Messages\" WHERE \"ConversationId\" = {conversationId} AND (\"SentAt\", \"Id\") > ({anchor.SentAt}, {anchor.Id})").AsNoTracking();
            var list = await query.OrderBy(x => x.SentAt).ThenBy(x => x.Id)
                .Take(limit + 1).ToListAsync(ct);
            var hasMore = list.Count > limit;
            return new MessagesPage(list.Take(limit).Select(MessageDto.From).ToList(), null,
                hasMore ? list[limit - 1].Id : null);
        }
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            if (!CursorCodec.TryDecode(cursor, out var sentAt, out var id))
                throw new ChatDomainException(400, "Cursor không hợp lệ.");
            query = db.Messages.FromSqlInterpolated(
                $"SELECT * FROM \"Messages\" WHERE \"ConversationId\" = {conversationId} AND (\"SentAt\", \"Id\") < ({sentAt}, {id})").AsNoTracking();
        }
        var older = await query.OrderByDescending(x => x.SentAt).ThenByDescending(x => x.Id)
            .Take(limit + 1).ToListAsync(ct);
        var more = older.Count > limit;
        var pageItems = older.Take(limit).ToList();
        var next = more && pageItems.Count != 0
            ? CursorCodec.Encode(pageItems[^1].SentAt, pageItems[^1].Id) : null;
        pageItems.Reverse();
        return new MessagesPage(pageItems.Select(MessageDto.From).ToList(), next, null);
    }

    public async Task<SendResult> SendMessageAsync(Guid conversationId, Guid senderId,
        string? content, Guid clientMessageId, CancellationToken ct = default)
    {
        content = MessageRules.Validate(content, clientMessageId);

        var access = await permissions.GetAccessAsync(conversationId, senderId, ct);
        if (!access.CanSend) throw new ChatDomainException(403, "Không có quyền gửi tin trong hội thoại.");

        // Unique(SenderId, ClientMessageId) provides idempotency across retries.
        var existing = await db.Messages.AsNoTracking()
            .SingleOrDefaultAsync(x => x.SenderId == senderId && x.ClientMessageId == clientMessageId, ct);
        if (existing is not null)
        {
            if (existing.ConversationId != conversationId || existing.Content != content)
                throw new ChatDomainException(409, "clientMessageId đã dùng với nội dung khác.");
            return new SendResult(MessageDto.From(existing), false, access.PartnerId, null);
        }

        // Serializable helps detect concurrent relationship updates IF TV3/TV5 also
        // use compatible transactional writes. Their policies must be merged here.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            access = await permissions.GetAccessAsync(conversationId, senderId, ct);
            if (!access.CanSend) throw new ChatDomainException(403, "Quyền gửi tin đã thay đổi.");
            var entity = new ChatMessage
            {
                ConversationId = conversationId,
                SenderId = senderId,
                ClientMessageId = clientMessageId,
                Content = content
            };
            db.Messages.Add(entity);
            var notif = notifications.Queue(access.PartnerId, "newMessage", conversationId);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct); // publish realtime ONLY after commit
            return new SendResult(MessageDto.From(entity), true, access.PartnerId,
                NotificationDto.From(notif));
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pe
            && pe.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            var duplicate = await db.Messages.AsNoTracking().SingleOrDefaultAsync(x =>
                x.SenderId == senderId && x.ClientMessageId == clientMessageId, ct);
            if (duplicate is null) throw new ChatDomainException(409, "Xung đột dữ liệu; hãy thử lại.");
            if (duplicate.ConversationId != conversationId || duplicate.Content != content)
                throw new ChatDomainException(409, "clientMessageId đã dùng với nội dung khác.");
            return new SendResult(MessageDto.From(duplicate), false, access.PartnerId, null);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pe
            && pe.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            await tx.RollbackAsync(ct);
            throw new ChatDomainException(409, "Có thay đổi đồng thời; vui lòng gửi lại.");
        }
    }
}
