using Microsoft.EntityFrameworkCore;
using DatingApp.Api.Modules.DiscoveryMatching.Entities;

namespace DatingApp.Api.Infrastructure.Data;

// TV1 will extend this to IdentityDbContext when implementing Auth.
// Add entities + reviewed migrations together; never use EnsureCreated in production.
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UserLike> UserLikes => Set<UserLike>();
    public DbSet<Pass> Passes => Set<Pass>();
    public DbSet<Match> Matches => Set<Match>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
