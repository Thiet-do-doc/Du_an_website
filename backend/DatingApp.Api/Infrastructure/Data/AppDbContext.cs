using DatingApp.Api.Modules.Auth.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.Api.Infrastructure.Data;

public sealed class AppDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Áp dụng cấu hình bảng và quan hệ mặc định của Identity.
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<ApplicationUser>().Property(x => x.IsActive).HasDefaultValue(true);
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.Property(x => x.TokenHash).HasMaxLength(64);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => x.SessionId);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // Áp dụng các lớp cấu hình entity riêng khi nhóm bổ sung sau này.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);
    }
}
