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
        b.HasOne(x => x.FromUser).WithMany().HasForeignKey(x => x.FromUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ToUser).WithMany().HasForeignKey(x => x.ToUserId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => t.HasCheckConstraint("CK_Passes_NoSelfPass", "\"FromUserId\" <> \"ToUserId\""));
    }
}
