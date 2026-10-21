using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.Auth.Entities;
using DatingApp.Api.Modules.Auth.Services;
using DatingApp.Api.Modules.SafetyAdmin.Entities;
using DatingApp.Api.Modules.SafetyAdmin.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DatingApp.Api.Tests;

public sealed class BlockEndpointTests
{
    [PostgresFact]
    public async Task BlocksAreOwnedIdempotentAndBidirectionalWithRealJwt()
    {
        await using var app = new BlockApp();
        var a = await Seed(app); var b = await Seed(app); var c = await Seed(app);
        using var alice = await Login(app, a); using var bob = await Login(app, b);
        using var anonymous = app.CreateClient();
        var path = $"/api/v1/blocks/{b.Id}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync(path, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/blocks")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PostAsync("/api/v1/blocks/not-a-guid", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PostAsync($"/api/v1/blocks/{Guid.Empty}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PostAsync($"/api/v1/blocks/{a.Id}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.DeleteAsync($"/api/v1/blocks/{a.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.PostAsync($"/api/v1/blocks/{Guid.NewGuid()}", null)).StatusCode);
        // Body/query cannot choose the actor. The server always uses CurrentUser.
        Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsJsonAsync(path + $"?blockerUserId={c.Id}", new { blockerUserId = c.Id })).StatusCode);
        var repeats = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => alice.PostAsync(path, null)));
        Assert.All(repeats, response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var policy = scope.ServiceProvider.GetRequiredService<IBlockPolicy>();
        Assert.Equal(1, await db.Blocks.CountAsync(x => x.BlockerUserId == a.Id && x.BlockedUserId == b.Id));
        Assert.False(await db.Blocks.AnyAsync(x => x.BlockerUserId == c.Id));
        Assert.False(await policy.CanInteractAsync(a.Id, b.Id));
        Assert.False(await policy.CanInteractAsync(b.Id, a.Id));
        Assert.True(await policy.CanInteractAsync(a.Id, c.Id));
        Assert.False(await policy.CanInteractAsync(Guid.Empty, b.Id));
        var list = await alice.GetFromJsonAsync<JsonElement[]>("/api/v1/blocks");
        Assert.Contains(list!, item => item.GetProperty("userId").GetGuid() == b.Id);
        Assert.Empty((await bob.GetFromJsonAsync<JsonElement[]>("/api/v1/blocks"))!);
        Assert.Equal(HttpStatusCode.NoContent, (await bob.DeleteAsync($"/api/v1/blocks/{a.Id}?blockerUserId={a.Id}")).StatusCode);
        Assert.False(await policy.CanInteractAsync(a.Id, b.Id));
        Assert.Equal(HttpStatusCode.NoContent, (await bob.PostAsync($"/api/v1/blocks/{a.Id}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await alice.DeleteAsync(path)).StatusCode);
        Assert.False(await policy.CanInteractAsync(a.Id, b.Id)); // Bob still owns a reverse block.
        var deletes = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => bob.DeleteAsync($"/api/v1/blocks/{a.Id}")));
        Assert.All(deletes, response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));
        Assert.True(await policy.CanInteractAsync(a.Id, b.Id));
        Assert.Empty((await alice.GetFromJsonAsync<JsonElement[]>("/api/v1/blocks"))!);
        // API never creates or updates a match; no match module is coupled here.
    }

    [PostgresFact]
    public async Task PostgresRejectsSelfBlockDuplicateAndMissingUser()
    {
        await using var app = new BlockApp(); var a = await Seed(app); var b = await Seed(app);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Blocks.Add(new UserBlock { BlockerUserId = a.Id, BlockedUserId = a.Id });
        var self = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("23514", Assert.IsType<Npgsql.PostgresException>(self.InnerException).SqlState); db.ChangeTracker.Clear();
        db.Blocks.Add(new UserBlock { BlockerUserId = a.Id, BlockedUserId = Guid.NewGuid() });
        var missing = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("23503", Assert.IsType<Npgsql.PostgresException>(missing.InnerException).SqlState); db.ChangeTracker.Clear();
        db.Blocks.Add(new UserBlock { BlockerUserId = a.Id, BlockedUserId = b.Id }); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        db.Blocks.Add(new UserBlock { BlockerUserId = a.Id, BlockedUserId = b.Id });
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("23505", Assert.IsType<Npgsql.PostgresException>(duplicate.InnerException).SqlState);
    }
    private static async Task<ApplicationUser> Seed(BlockApp app)
    {
        using var scope = app.Services.CreateScope(); var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"block-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, DateOfBirth = new DateOnly(2000, 1, 1) };
        Assert.True((await users.CreateAsync(user, "BlockTest!2026")).Succeeded); return user;
    }
    private static async Task<HttpClient> Login(BlockApp app, ApplicationUser user)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(new { email = user.Email, password = "BlockTest!2026" }) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        var response = await client.SendAsync(request); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.GetProperty("token").GetString());
        return client;
    }
    private sealed class BlockApp : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing")
            .UseSetting("ConnectionStrings:DefaultConnection", Environment.GetEnvironmentVariable("AUTH_TEST_CONNECTION"))
            .UseSetting("Jwt:Key", "block-integration-test-only-key-at-least-32-bytes")
            .UseSetting("Jwt:Issuer", "block-tests").UseSetting("Jwt:Audience", "block-tests")
            .UseSetting("Jwt:ExpireMinutes", "10").UseSetting("Auth:FrontendUrl", "https://frontend.test");
    }
}
