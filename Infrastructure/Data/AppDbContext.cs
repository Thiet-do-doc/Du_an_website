using DatingApp.Api.Modules.Auth.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.Api.Infrastructure.Data;

// Shared DbContext: Identity tables and future modules use the same database.
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<ApplicationUser>().Property(x => x.DateOfBirth).HasColumnType("date");
        modelBuilder.Entity<ApplicationUser>().HasIndex(x => x.NormalizedEmail).IsUnique();
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
