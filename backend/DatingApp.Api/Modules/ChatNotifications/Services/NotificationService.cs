using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.ChatNotifications.DTOs;
using DatingApp.Api.Modules.ChatNotifications.Entities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using DatingApp.Api.Modules.ChatNotifications.Hubs;

namespace DatingApp.Api.Modules.ChatNotifications.Services;

public class NotificationService(AppDbContext db, IHubContext<ChatHub> hub)
{
    // Called BEFORE the enclosing SaveChanges/transaction commit; publishing is separate.
    public AppNotification Queue(Guid recipientId, string type, Guid targetId)
    {
        var n = new AppNotification { RecipientId = recipientId, Type = type, TargetId = targetId };
        db.Notifications.Add(n);
        return n;
    }

    public async Task PublishAsync(AppNotification n, CancellationToken ct = default)
    {
        // This event is non-sensitive metadata and targets the owning user only.
        await hub.Clients.User(n.RecipientId.ToString()).SendAsync("NotificationCreated",
            NotificationDto.From(n), ct);
    }

    public async Task<NotificationsPage> ListAsync(Guid userId, int page, int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var entities = await db.Notifications.AsNoTracking()
            .Where(x => x.RecipientId == userId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize + 1).ToListAsync(ct);
        return new NotificationsPage(entities.Take(pageSize).Select(NotificationDto.From).ToList(),
            page, pageSize, entities.Count > pageSize);
    }

    public async Task<NotificationDto> MarkReadAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var n = await db.Notifications.SingleOrDefaultAsync(x => x.Id == id && x.RecipientId == userId, ct);
        if (n is null) throw new ChatDomainException(404, "Không tìm thấy thông báo.");
        n.ReadAt ??= DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return NotificationDto.From(n);
    }
}
