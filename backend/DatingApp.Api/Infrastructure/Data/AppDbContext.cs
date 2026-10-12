using Microsoft.EntityFrameworkCore;
using DatingApp.Api.Modules.SafetyAdmin.Entities;

namespace DatingApp.Api.Infrastructure.Data;

// TV1 will extend this to IdentityDbContext when implementing Auth.
// Add entities + reviewed migrations together; never use EnsureCreated in production.
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
