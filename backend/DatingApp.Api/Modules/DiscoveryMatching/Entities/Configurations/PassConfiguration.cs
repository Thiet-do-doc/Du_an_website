using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DatingApp.Api.Modules.DiscoveryMatching.Entities.Configurations;

public sealed class PassConfiguration : IEntityTypeConfiguration<Pass>
{
    public void Configure(EntityTypeBuilder<Pass> b)
    {
        b.ToTable("Passes");
        b.HasKey(x => new { x.FromUserId, x.ToUserId });
        b.Property(x => x.CreatedAt).IsRequired();
        b.ToTable(t => t.HasCheckConstraint("CK_Passes_NoSelfPass", "\"FromUserId\" <> \"ToUserId\""));
    }
}
