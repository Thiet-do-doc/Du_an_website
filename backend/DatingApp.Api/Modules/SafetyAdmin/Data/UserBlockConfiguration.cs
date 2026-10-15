using DatingApp.Api.Modules.SafetyAdmin.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DatingApp.Api.Modules.SafetyAdmin.Data;

public sealed class UserBlockConfiguration : IEntityTypeConfiguration<UserBlock>
{
    public void Configure(EntityTypeBuilder<UserBlock> builder)
    {
        builder.ToTable("user_blocks");
        builder.HasKey(block => new { block.BlockerUserId, block.BlockedUserId });
        builder.Property(block => block.BlockerUserId).HasColumnName("blocker_user_id");
        builder.Property(block => block.BlockedUserId).HasColumnName("blocked_user_id");
        builder.Property(block => block.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.HasIndex(block => block.BlockedUserId);
        builder.ToTable(table => table.HasCheckConstraint(
            "ck_user_blocks_no_self_block", "blocker_user_id <> blocked_user_id"));
    }
}
