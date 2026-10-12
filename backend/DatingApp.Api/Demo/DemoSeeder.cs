using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.ChatNotifications.Entities;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.Api.Demo;

public static class DemoSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (!await db.DemoUsers.AnyAsync())
        {
            db.DemoUsers.AddRange(
                new DemoUser { Id = DemoIds.A, Alias = "A", DisplayName = "Người dùng A" },
                new DemoUser { Id = DemoIds.B, Alias = "B", DisplayName = "Người dùng B" },
                new DemoUser { Id = DemoIds.C, Alias = "C", DisplayName = "Người dùng C" });
            await db.SaveChangesAsync();
        }
        if (!await db.DemoMatches.AnyAsync(m => m.Id == DemoIds.AB))
        {
            db.DemoMatches.Add(new DemoMatch { Id = DemoIds.AB, UserAId = DemoIds.A, UserBId = DemoIds.B });
            await db.SaveChangesAsync();
        }
        if (!await db.Conversations.AnyAsync(c => c.MatchId == DemoIds.AB))
        {
            db.Conversations.Add(new Conversation { MatchId = DemoIds.AB });
            db.Notifications.AddRange(
                new AppNotification { RecipientId = DemoIds.A, Type = "match", TargetId = DemoIds.AB },
                new AppNotification { RecipientId = DemoIds.B, Type = "match", TargetId = DemoIds.AB });
            await db.SaveChangesAsync();
        }
    }
}
