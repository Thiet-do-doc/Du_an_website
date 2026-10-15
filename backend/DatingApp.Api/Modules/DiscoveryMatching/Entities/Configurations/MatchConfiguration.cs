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
        b.ToTable(t => t.HasCheckConstraint("CK_Matches_UserOrder", "\"UserLowId\" < \"UserHighId\""));
    }
}
