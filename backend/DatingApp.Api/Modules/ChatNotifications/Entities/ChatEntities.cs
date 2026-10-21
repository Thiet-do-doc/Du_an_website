namespace DatingApp.Api.Modules.ChatNotifications.Entities;

/// <summary>
/// One conversation is created for one real Match. MatchId is deliberately a GUID contract;
/// the FK to the TV3 Matches table is added only after TV3's Match model is present in the
/// shared AppDbContext and the team generates/reviews the combined migration.
/// </summary>
public sealed class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MatchId { get; set; }
    public Guid UserLowId { get; set; }
    public Guid UserHighId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public Guid ClientMessageId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AppNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecipientId { get; set; }
    public Guid ConversationId { get; set; }
    // "match" or "newMessage". TargetId is the MatchId or persisted MessageId respectively.
    public string Type { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    // Stable per-recipient event key makes duplicate publish/retry idempotent.
    public string DedupeKey { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }
}
