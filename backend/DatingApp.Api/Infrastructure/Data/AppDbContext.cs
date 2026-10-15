using DatingApp.Api.Modules.Auth.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using DatingApp.Api.Modules.SafetyAdmin.Entities;

namespace DatingApp.Api.Infrastructure.Data;

// Shared DbContext: Identity tables and future modules use the same database.
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<UserBlock> Blocks => Set<UserBlock>();
    public DbSet<UserReport> Reports => Set<UserReport>();
    public DbSet<ModerationAction> ModerationActions => Set<ModerationAction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<ApplicationUser>().Property(x => x.DateOfBirth).HasColumnType("date");
        modelBuilder.Entity<ApplicationUser>().HasIndex(x => x.NormalizedEmail).IsUnique();
        modelBuilder.Entity<UserBlock>().HasIndex(x => new { x.BlockerUserId, x.BlockedUserId }).IsUnique();
        modelBuilder.Entity<UserBlock>().HasIndex(x => x.BlockedUserId);
        modelBuilder.Entity<UserReport>().HasIndex(x => new { x.Status, x.CreatedAtUtc });
        modelBuilder.Entity<ModerationAction>().HasIndex(x => new { x.TargetUserId, x.CreatedAtUtc });
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
