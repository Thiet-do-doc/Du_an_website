using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.Auth.Entities;
using DatingApp.Api.Modules.ChatNotifications.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DatingApp.Api.Tests;

public sealed partial class AuthFlowTests
{
    [PostgresFact]
    public async Task RealModulesConnectProfilesMatchChatBlockReportAndAccountRevocation()
    {
        await using var app = new AuthApp();
        using var a = app.Browser(); using var b = app.Browser(); using var c = app.Browser();
        var alice = await RegisterConfirmed(app, a); var bob = await RegisterConfirmed(app, b);
        var outsider = await RegisterConfirmed(app, c);
        var at = await Token(await a.Post("login", new { alice.Email, Password }));
        var bt = await Token(await b.Post("login", new { bob.Email, Password }));
        var ct = await Token(await c.Post("login", new { outsider.Email, Password }));
        var city = "integration-" + Guid.NewGuid().ToString("N");
        foreach (var (browser, token, name) in new[] { (a, at, "Alice"), (b, bt, "Bob"), (c, ct, "Carol") })
            Assert.Equal(HttpStatusCode.OK, (await browser.PutApi("profiles/me", new
            { displayName = name, city, bio = "Integration test", isDiscoverable = true, interestIds = new[] { SampleInterests[0] } }, token)).StatusCode);

        var photoId = Guid.NewGuid();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Photos.Add(new DatingApp.Api.Modules.Profiles.Entities.Photo
                { Id = photoId, UserId = alice.Id, Content = [1, 2, 3], ContentType = "image/png", IsAvatar = true });
            await db.SaveChangesAsync();
        }
        var photoPath = $"photos/{photoId}/content";
        Assert.Equal(HttpStatusCode.OK, (await b.GetApi(photoPath, bt)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.GetApi(photoPath, "")).StatusCode);

        var page1 = await (await a.GetApi($"discover?city={city}&pageSize=1&page=1", at)).Content.ReadFromJsonAsync<JsonElement>();
        var page2 = await (await a.GetApi($"discover?city={city}&pageSize=1&page=2", at)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(page1.GetProperty("hasMore").GetBoolean());
        Assert.Single(page2.GetProperty("items").EnumerateArray());
        Assert.NotEqual(page1.GetProperty("items")[0].GetProperty("userId").GetGuid(), page2.GetProperty("items")[0].GetProperty("userId").GetGuid());
        Assert.Equal(HttpStatusCode.OK, (await a.WriteApi(HttpMethod.Post, $"user-likes/{bob.Id}", new { }, at)).StatusCode);
        var mutual = await b.WriteApi(HttpMethod.Post, $"user-likes/{alice.Id}", new { }, bt);
        Assert.Equal(HttpStatusCode.OK, mutual.StatusCode);
        var match = await mutual.Content.ReadFromJsonAsync<JsonElement>();
        var conversationId = match.GetProperty("conversationId").GetGuid();
        var matchId = match.GetProperty("matchId").GetGuid();
        Assert.NotEqual(Guid.Empty, conversationId);
        var conversations = await (await a.GetApi("conversations", at)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Bob", conversations.GetProperty("items")[0].GetProperty("partnerName").GetString());
        Guid messageId;
        using (var scope = app.Services.CreateScope())
        {
            var chat = scope.ServiceProvider.GetRequiredService<ChatService>();
            var clientId = Guid.NewGuid();
            var sent = await chat.SendMessageAsync(conversationId, alice.Id, "Hello from integrated modules", clientId);
            var replay = await chat.SendMessageAsync(conversationId, alice.Id, "Hello from integrated modules", clientId);
            messageId = sent.Message.Id;
            Assert.Equal(messageId, replay.Message.Id); Assert.False(replay.IsNew);
        }
        Assert.Equal(HttpStatusCode.OK, (await b.GetApi($"conversations/{conversationId}/messages", bt)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetApi($"conversations/{conversationId}/messages", ct)).StatusCode);
        var report = new { targetType = "Message", targetId = messageId, reason = "Test report", description = "Integration test only" };
        Assert.Equal(HttpStatusCode.Created, (await b.WriteApi(HttpMethod.Post, "reports", report, bt)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.WriteApi(HttpMethod.Post, "reports", report, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.WriteApi(HttpMethod.Post, "reports", report, at)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await a.GetApi("admin/reports", at)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await b.WriteApi(HttpMethod.Post, $"blocks/{alice.Id}", new { }, bt)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetApi(photoPath, bt)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetApi($"profiles/{alice.Id}", bt)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetApi($"profiles/{bob.Id}", at)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.GetApi(photoPath, at)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetApi($"conversations/{conversationId}/messages", at)).StatusCode);
        Assert.Empty((await (await a.GetApi("matches", at)).Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        using (var scope = app.Services.CreateScope())
            Assert.Equal(403, (await Assert.ThrowsAsync<ChatDomainException>(() => scope.ServiceProvider
                .GetRequiredService<ChatService>().SendMessageAsync(conversationId, alice.Id, "blocked", Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await b.WriteApi(HttpMethod.Post, "reports", report, bt)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await b.WriteApi(HttpMethod.Delete, $"blocks/{alice.Id}", null, bt)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a.WriteApi(HttpMethod.Delete, $"matches/{matchId}", null, at)).StatusCode);
        using (var scope = app.Services.CreateScope())
            Assert.Equal(403, (await Assert.ThrowsAsync<ChatDomainException>(() => scope.ServiceProvider
                .GetRequiredService<ChatService>().SendMessageAsync(conversationId, alice.Id, "unmatched", Guid.NewGuid()))).StatusCode);

        using (var scope = app.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            if (!await roles.RoleExistsAsync("Admin")) await roles.CreateAsync(new IdentityRole<Guid>("Admin"));
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(outsider.Id.ToString()))!, "Admin")).Succeeded);
        }
        Assert.Equal(HttpStatusCode.OK, (await c.WriteApi(HttpMethod.Patch, $"admin/users/{alice.Id}/status", new
            { isActive = false, reason = "Integration test suspension" }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.Get("me", at)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.Post("refresh")).StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.True(await db.ModerationActions.AnyAsync(x => x.TargetUserId == alice.Id.ToString()));
            Assert.False(await db.RefreshTokens.AnyAsync(x => x.UserId == alice.Id && x.RevokedAtUtc == null));
        }
    }

    private sealed partial class Browser
    {
        public Task<HttpResponseMessage> WriteApi(HttpMethod method, string path, object? body, string bearer) =>
            ApiSend(method, path, body, bearer, null);
    }
}
