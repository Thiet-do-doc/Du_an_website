using DatingApp.Api.Demo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DatingApp.Api.Modules.ChatNotifications.Entities;

public sealed class ConversationConfig : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> e)
    {
        e.HasKey(x => x.Id);
        e.HasIndex(x => x.MatchId).IsUnique();
        e.HasOne<DemoMatch>().WithMany().HasForeignKey(x => x.MatchId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class MessageConfig : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> e)
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.Content).HasMaxLength(2000).IsRequired();
        e.HasIndex(x => new { x.ConversationId, x.SentAt, x.Id });
        e.HasIndex(x => new { x.SenderId, x.ClientMessageId }).IsUnique();
        e.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<DemoUser>().WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class NotificationConfig : IEntityTypeConfiguration<AppNotification>
{
    public void Configure(EntityTypeBuilder<AppNotification> e)
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.Type).HasMaxLength(40).IsRequired();
        e.HasIndex(x => new { x.RecipientId, x.ReadAt });
        e.HasIndex(x => new { x.RecipientId, x.CreatedAt, x.Id });
        e.HasOne<DemoUser>().WithMany().HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class DemoUserConfig : IEntityTypeConfiguration<DemoUser>
{
    public void Configure(EntityTypeBuilder<DemoUser> e)
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.Alias).HasMaxLength(20);
        e.Property(x => x.DisplayName).HasMaxLength(100);
        e.HasIndex(x => x.Alias).IsUnique();
    }
}
public sealed class DemoMatchConfig : IEntityTypeConfiguration<DemoMatch>
{
    public void Configure(EntityTypeBuilder<DemoMatch> e)
    {
        e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.UserAId, x.UserBId }).IsUnique();
        e.HasOne<DemoUser>().WithMany().HasForeignKey(x => x.UserAId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<DemoUser>().WithMany().HasForeignKey(x => x.UserBId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class DemoBlockConfig : IEntityTypeConfiguration<DemoBlock>
{
    public void Configure(EntityTypeBuilder<DemoBlock> e)
    {
        e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.BlockerId, x.BlockedId }).IsUnique();
        e.HasOne<DemoUser>().WithMany().HasForeignKey(x => x.BlockerId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<DemoUser>().WithMany().HasForeignKey(x => x.BlockedId).OnDelete(DeleteBehavior.Restrict);
    }
}
