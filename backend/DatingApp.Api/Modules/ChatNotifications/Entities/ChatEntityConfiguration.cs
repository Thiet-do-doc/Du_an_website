using DatingApp.Api.Modules.Auth.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DatingApp.Api.Modules.ChatNotifications.Entities;

public sealed class ConversationConfig : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> e)
    {
        e.ToTable("Conversations", table => table.HasCheckConstraint(
            "CK_Conversations_UserOrder", "\"UserLowId\" < \"UserHighId\""));
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.HasIndex(x => x.MatchId).IsUnique();
        e.HasIndex(x => new { x.UserLowId, x.CreatedAt });
        e.HasIndex(x => new { x.UserHighId, x.CreatedAt });
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserLowId)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserHighId)
            .OnDelete(DeleteBehavior.Restrict);
        // The real Match FK is added by the shared migration after TV3's Match entity is merged.
    }
}

public sealed class MessageConfig : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> e)
    {
        e.ToTable("Messages");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.Content).HasMaxLength(2000).IsRequired();
        e.HasIndex(x => new { x.ConversationId, x.SentAt, x.Id });
        e.HasIndex(x => new { x.SenderId, x.ClientMessageId }).IsUnique();
        e.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.SenderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class NotificationConfig : IEntityTypeConfiguration<AppNotification>
{
    public void Configure(EntityTypeBuilder<AppNotification> e)
    {
        e.ToTable("Notifications");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.Type).HasMaxLength(40).IsRequired();
        e.Property(x => x.DedupeKey).HasMaxLength(120).IsRequired();
        e.HasIndex(x => new { x.RecipientId, x.DedupeKey }).IsUnique();
        e.HasIndex(x => new { x.RecipientId, x.ReadAt });
        e.HasIndex(x => new { x.RecipientId, x.CreatedAt, x.Id });
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
