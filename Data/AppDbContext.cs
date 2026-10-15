using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Vo_T1_2_AuthStarter.Models;

namespace Vo_T1_2_AuthStarter.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>().Property(x => x.DateOfBirth).HasColumnType("date");
        builder.Entity<ApplicationUser>().HasIndex(x => x.NormalizedEmail).IsUnique();
    }
}
