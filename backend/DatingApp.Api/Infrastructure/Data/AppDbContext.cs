using DatingApp.Api.Demo;
using DatingApp.Api.Modules.ChatNotifications.Entities;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.Api.Infrastructure.Data;

// Shared DbContext for all five modules. TV1 will extend this to IdentityDbContext.
// TV4 demo tables are temporary and must be removed when real Auth/Matching/Block are merged.
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();
    public DbSet<AppNotification> Notifications => Set<AppNotification>();

    // DEMO ONLY adapters to test TV4 before TV1, TV3 and TV5 finish.
    public DbSet<DemoUser> DemoUsers => Set<DemoUser>();
    public DbSet<DemoMatch> DemoMatches => Set<DemoMatch>();
    public DbSet<DemoBlock> DemoBlocks => Set<DemoBlock>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
