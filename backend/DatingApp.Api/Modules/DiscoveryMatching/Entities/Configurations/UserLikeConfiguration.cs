using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DatingApp.Api.Modules.DiscoveryMatching.Entities.Configurations;

public sealed class UserLikeConfiguration : IEntityTypeConfiguration<UserLike>
{
    public void Configure(EntityTypeBuilder<UserLike> b)
    {
        b.ToTable("UserLikes");
        b.HasKey(x => new { x.FromUserId, x.ToUserId });
        b.Property(x => x.CreatedAt).IsRequired();
        b.HasIndex(x => new { x.ToUserId, x.FromUserId });
        b.ToTable(t => t.HasCheckConstraint("CK_UserLikes_NoSelfLike", "\"FromUserId\" <> \"ToUserId\""));
    }
}
