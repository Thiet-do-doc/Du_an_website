using DatingApp.Api.Modules.ChatNotifications.Entities;

namespace DatingApp.Api.Modules.ChatNotifications.DTOs;

public record MessageDto(Guid Id, Guid ConversationId, Guid SenderId, Guid ClientMessageId,
    string Content, DateTimeOffset SentAt)
{
    public static MessageDto From(ChatMessage m) =>
        new(m.Id, m.ConversationId, m.SenderId, m.ClientMessageId, m.Content, m.SentAt);
}

public record ConversationDto(Guid Id, Guid MatchId, Guid PartnerId, string PartnerName,
    DateTimeOffset CreatedAt, MessageDto? LastMessage, bool CanSend);
public record MessagesPage(IReadOnlyList<MessageDto> Items, string? NextCursor, Guid? NextAfterId);
public record NotificationDto(Guid Id, string Type, Guid TargetId, DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt)
{
    public static NotificationDto From(AppNotification n) =>
        new(n.Id, n.Type, n.TargetId, n.CreatedAt, n.ReadAt);
}
public record NotificationsPage(IReadOnlyList<NotificationDto> Items, int Page, int PageSize, bool HasMore);
public record ConversationPage(IReadOnlyList<ConversationDto> Items, int Page, int PageSize, bool HasMore);
