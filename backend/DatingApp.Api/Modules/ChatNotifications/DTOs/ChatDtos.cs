using DatingApp.Api.Modules.ChatNotifications.Entities;

namespace DatingApp.Api.Modules.ChatNotifications.DTOs;

public sealed record MessageDto(Guid Id, Guid ConversationId, Guid SenderId, Guid ClientMessageId,
    string Content, DateTimeOffset SentAt)
{
    public static MessageDto From(ChatMessage m) =>
        new(m.Id, m.ConversationId, m.SenderId, m.ClientMessageId, m.Content, m.SentAt);
}

public sealed record ConversationDto(Guid Id, Guid MatchId, Guid PartnerId, string PartnerName,
    DateTimeOffset CreatedAt, MessageDto? LastMessage, bool CanSend);
public sealed record MessagesPage(IReadOnlyList<MessageDto> Items, string? NextCursor, Guid? NextAfterId);
public sealed record NotificationDto(Guid Id, string Type, Guid TargetId, Guid ConversationId,
    DateTimeOffset CreatedAt, DateTimeOffset? ReadAt)
{
    public static NotificationDto From(AppNotification n) =>
        new(n.Id, n.Type, n.TargetId, n.ConversationId, n.CreatedAt, n.ReadAt);
}
public sealed record NotificationsPage(IReadOnlyList<NotificationDto> Items, int Page, int PageSize,
    bool HasMore, string? NextCursor = null, Guid? NextAfterId = null);
public sealed record ConversationPage(IReadOnlyList<ConversationDto> Items, int Page, int PageSize, bool HasMore);
