using DatingApp.Api.Modules.Auth.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using DatingApp.Api.Modules.Profiles.Entities;
using DatingApp.Api.Modules.DiscoveryMatching.Entities;

namespace DatingApp.Api.Infrastructure.Data;

public sealed class AppDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<Interest> Interests => Set<Interest>();
    public DbSet<UserInterest> UserInterests => Set<UserInterest>();
    public DbSet<UserLike> UserLikes => Set<UserLike>();
    public DbSet<Pass> Passes => Set<Pass>();
    public DbSet<Match> Matches => Set<Match>();
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
        modelBuilder.Entity<Profile>(entity =>
        {
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.DisplayName).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Bio).HasMaxLength(500);
            entity.Property(x => x.City).HasMaxLength(80);
            entity.Property(x => x.Gender).HasMaxLength(30);
            entity.HasOne(x => x.User).WithOne().HasForeignKey<Profile>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Photo>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ContentType).HasMaxLength(30).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.SortOrder }).IsUnique();
            entity.HasIndex(x => x.UserId).IsUnique().HasFilter("\"IsAvatar\" = TRUE");
            entity.HasOne(x => x.Profile).WithMany(x => x.Photos).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Interest>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(60).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasData(
                new Interest { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), Name = "Du lịch" },
                new Interest { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Name = "Ẩm thực" },
                new Interest { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), Name = "Âm nhạc" },
                new Interest { Id = Guid.Parse("10000000-0000-0000-0000-000000000004"), Name = "Phim ảnh" },
                new Interest { Id = Guid.Parse("10000000-0000-0000-0000-000000000005"), Name = "Đọc sách" },
                new Interest { Id = Guid.Parse("10000000-0000-0000-0000-000000000006"), Name = "Thể thao" },
                new Interest { Id = Guid.Parse("10000000-0000-0000-0000-000000000007"), Name = "Chạy bộ" },
                new Interest { Id = Guid.Parse("10000000-0000-0000-0000-000000000008"), Name = "Nấu ăn" },
                new Interest { Id = Guid.Parse("10000000-0000-0000-0000-000000000009"), Name = "Nhiếp ảnh" },
                new Interest { Id = Guid.Parse("10000000-0000-0000-0000-000000000010"), Name = "Cà phê" },
                new Interest { Id = Guid.Parse("10000000-0000-0000-0000-000000000011"), Name = "Thiên nhiên" },
                new Interest { Id = Guid.Parse("10000000-0000-0000-0000-000000000012"), Name = "Công nghệ" },
                new Interest { Id = Guid.Parse("10000000-0000-0000-0000-000000000013"), Name = "Thú cưng" },
                new Interest { Id = Guid.Parse("10000000-0000-0000-0000-000000000014"), Name = "Nghệ thuật" },
                new Interest { Id = Guid.Parse("10000000-0000-0000-0000-000000000015"), Name = "Yoga" });
        });
        modelBuilder.Entity<UserInterest>(entity =>
        {
            entity.HasKey(x => new { x.UserId, x.InterestId });
            entity.HasOne(x => x.Profile).WithMany(x => x.UserInterests).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Interest).WithMany(x => x.UserInterests).HasForeignKey(x => x.InterestId).OnDelete(DeleteBehavior.Cascade);
        });

        // Áp dụng các lớp cấu hình entity riêng khi nhóm bổ sung sau này.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);
    }
}
