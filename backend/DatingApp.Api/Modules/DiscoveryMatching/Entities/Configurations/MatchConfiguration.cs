using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DatingApp.Api.Modules.DiscoveryMatching.Entities.Configurations;

public sealed class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> b)
    {
        b.ToTable("Matches");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.MatchedAt).IsRequired();
        b.HasIndex(x => new { x.UserLowId, x.UserHighId }).IsUnique();
        b.HasIndex(x => new { x.UserLowId, x.UnmatchedAt });
        b.HasIndex(x => new { x.UserHighId, x.UnmatchedAt });
        b.HasOne(x => x.LowUser).WithMany().HasForeignKey(x => x.UserLowId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.HighUser).WithMany().HasForeignKey(x => x.UserHighId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => t.HasCheckConstraint("CK_Matches_UserOrder", "\"UserLowId\" < \"UserHighId\""));
    }
}
