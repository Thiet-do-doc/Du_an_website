using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.ChatNotifications.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace DatingApp.Api.Demo;

public static class DevelopmentChatEndpoints
{
    // ONLY invoked in Development and ONLY meant for local fake data.
    public static void MapDevEndpoints(this WebApplication app, string key, string issuer, string audience)
    {
    app.MapGet("/api/v1/dev/token/{alias}", async (string alias, AppDbContext db) =>
    {
        var user = await db.DemoUsers.SingleOrDefaultAsync(x => x.Alias == alias.ToUpper());
        if (user is null) return Results.NotFound();
        if (!user.IsActive) return Results.Forbid();
        var claims = new[] {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("demoAlias", user.Alias)
        };
        var jwt = new JwtSecurityToken(issuer, audience, claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256));
        return Results.Ok(new { token = new JwtSecurityTokenHandler().WriteToken(jwt),
            userId = user.Id, displayName = user.DisplayName });
    });

    // Used only to test TV4's integration hooks before TV3's real matching exists.
    app.MapPost("/api/v1/dev/match/{left}/{right}", async (
        string left, string right, AppDbContext db, MatchChatBridge bridge) =>
    {
        var a = await db.DemoUsers.SingleOrDefaultAsync(x => x.Alias == left.ToUpper());
        var b = await db.DemoUsers.SingleOrDefaultAsync(x => x.Alias == right.ToUpper());
        if (a is null || b is null || a.Id == b.Id) return Results.BadRequest();
        var first = a.Id.CompareTo(b.Id) < 0 ? a.Id : b.Id;
        var second = first == a.Id ? b.Id : a.Id;
        var existing = await db.DemoMatches.SingleOrDefaultAsync(x =>
            x.UserAId == first && x.UserBId == second);
        if (existing is not null) return Results.Ok(new { matchId = existing.Id, existing = true });
        await using var transaction = await db.Database.BeginTransactionAsync();
        var match = new DemoMatch { UserAId = first, UserBId = second };
        db.DemoMatches.Add(match);
        await db.SaveChangesAsync();
        var conversation = await bridge.EnsureConversationAsync(match.Id);
        var (na, nb) = bridge.QueueMatchNotifications(match.Id, first, second);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        await bridge.PublishMatchAsync(match.Id, first, second, na, nb);
        return Results.Ok(new { matchId = match.Id, conversationId = conversation.Id });
    });

    app.MapPost("/api/v1/dev/block/{from}/{to}", async (string from, string to, AppDbContext db) =>
    {
        var a = await db.DemoUsers.SingleOrDefaultAsync(x => x.Alias == from.ToUpper());
        var b = await db.DemoUsers.SingleOrDefaultAsync(x => x.Alias == to.ToUpper());
        if (a is null || b is null || a.Id == b.Id) return Results.BadRequest();
        if (!await db.DemoBlocks.AnyAsync(x => x.BlockerId == a.Id && x.BlockedId == b.Id))
        {
            db.DemoBlocks.Add(new DemoBlock { BlockerId = a.Id, BlockedId = b.Id });
            await db.SaveChangesAsync();
        }
        return Results.NoContent();
    });
    app.MapDelete("/api/v1/dev/block/{from}/{to}", async (string from, string to, AppDbContext db) =>
    {
        var a = await db.DemoUsers.SingleOrDefaultAsync(x => x.Alias == from.ToUpper());
        var b = await db.DemoUsers.SingleOrDefaultAsync(x => x.Alias == to.ToUpper());
        if (a is null || b is null) return Results.NotFound();
        await db.DemoBlocks.Where(x => x.BlockerId == a.Id && x.BlockedId == b.Id)
            .ExecuteDeleteAsync();
        return Results.NoContent();
    });
    app.MapPatch("/api/v1/dev/users/{alias}/status", async (
        string alias, bool active, AppDbContext db) =>
    {
        var user = await db.DemoUsers.SingleOrDefaultAsync(x => x.Alias == alias.ToUpper());
        if (user is null) return Results.NotFound();
        user.IsActive = active;
        await db.SaveChangesAsync();
        return Results.NoContent();
    });
    app.MapDelete("/api/v1/dev/matches/{id:guid}", async (Guid id, AppDbContext db) =>
    {
        var match = await db.DemoMatches.SingleOrDefaultAsync(x => x.Id == id);
        if (match is null) return Results.NotFound();
        match.IsActive = false;
        await db.SaveChangesAsync();
        return Results.NoContent();
    });
    }
}
