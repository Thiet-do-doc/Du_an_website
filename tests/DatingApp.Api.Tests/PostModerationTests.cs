using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.Auth.Entities;
using DatingApp.Api.Modules.Posts.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DatingApp.Api.Tests;

public sealed partial class AuthFlowTests
{
    [PostgresFact]
    public async Task PostReportsPreserveEvidenceAndAdminHideIsAuthorizedAndIdempotent()
    {
        await using var app = new AuthApp();
        using var a = app.Browser(); using var b = app.Browser(); using var admin = app.Browser();
        var alice = await RegisterConfirmed(app, a); var bob = await RegisterConfirmed(app, b);
        var moderator = await RegisterConfirmed(app, admin);
        var at = await Token(await a.Post("login", new { alice.Email, Password }));
        var bt = await Token(await b.Post("login", new { bob.Email, Password }));
        var mt = await Token(await admin.Post("login", new { moderator.Email, Password }));
        using (var scope = app.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            if (!await roles.RoleExistsAsync("Admin")) await roles.CreateAsync(new IdentityRole<Guid>("Admin"));
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(moderator.Id.ToString()))!, "Admin")).Succeeded);
        }
        foreach (var (browser, token, name) in new[] { (a, at, "Alice"), (b, bt, "Bob") })
            Assert.Equal(HttpStatusCode.OK, (await browser.PutApi("profiles/me", new { displayName = name, interestIds = new[] { SampleInterests[0] } }, token)).StatusCode);
        var created = await (await a.WriteApi(HttpMethod.Post, "posts", new { content = "Original evidence" }, at)).Content.ReadFromJsonAsync<JsonElement>();
        var postId = created.GetProperty("id").GetGuid();
        var path = $"posts/{postId}";
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var post = await db.Set<Post>().SingleAsync(p => p.Id == postId);
            post.PhotoContent = [1, 2, 3]; post.PhotoType = "image/png";
            await db.SaveChangesAsync();
        }
        var request = new { targetType = "Post", targetId = postId, reason = "Nội dung không phù hợp", description = "Please review this post" };
        Assert.Equal(HttpStatusCode.BadRequest, (await a.WriteApi(HttpMethod.Post, "reports", request, at)).StatusCode);
        var response = await b.WriteApi(HttpMethod.Post, "reports", request, bt);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reportId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var reportPath = $"admin/reports/{reportId}";
        Assert.Equal(HttpStatusCode.Forbidden, (await b.WriteApi(HttpMethod.Post, reportPath + "/evidence", null, bt)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.WriteApi(HttpMethod.Patch, path, new { content = "Not mine" }, bt)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.WriteApi(HttpMethod.Patch, path, new { content = "Edited content" }, at)).StatusCode);
        var evidence = await (await admin.WriteApi(HttpMethod.Post, reportPath + "/evidence", null, mt)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Original evidence", evidence.GetProperty("content").GetString());
        Assert.Equal("data:image/png;base64,AQID", evidence.GetProperty("photoUrl").GetString());
        var hide = new { reason = "Reviewed inappropriate content" };
        Assert.Equal(HttpStatusCode.Forbidden, (await b.WriteApi(HttpMethod.Post, reportPath + "/hide-post", hide, bt)).StatusCode);
        foreach (var result in await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => admin.WriteApi(HttpMethod.Post, reportPath + "/hide-post", hide, mt))))
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        foreach (var suffix in new[] { "", "/photo", "/comments" })
            Assert.Equal(HttpStatusCode.NotFound, (await b.GetApi(path + suffix, bt)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.WriteApi(HttpMethod.Patch, path, new { content = "Try to republish" }, at)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.WriteApi(HttpMethod.Post, "reports", request, bt)).StatusCode);
        var history = await (await admin.GetApi(reportPath + "/actions", mt)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(history.EnumerateArray(), row => row.GetProperty("action").GetString() == "PostHidden");
        Assert.Equal(HttpStatusCode.NoContent, (await a.WriteApi(HttpMethod.Delete, path, null, at)).StatusCode);
        evidence = await (await admin.WriteApi(HttpMethod.Post, reportPath + "/evidence", null, mt)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Original evidence", evidence.GetProperty("content").GetString());
        Assert.True(evidence.GetProperty("deleted").GetBoolean());

        var other = await (await a.WriteApi(HttpMethod.Post, "posts", new { content = "Blocked evidence" }, at)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.NoContent, (await b.WriteApi(HttpMethod.Post, $"blocks/{alice.Id}", null, bt)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.WriteApi(HttpMethod.Post, "reports", new { request.targetType, targetId = other.GetProperty("id").GetGuid(), request.reason, request.description }, bt)).StatusCode);
    }

    [PostgresFact]
    public async Task CommentDeletionAndReadAllRespectOwnership()
    {
        await using var app = new AuthApp();
        using var a = app.Browser(); using var b = app.Browser();
        var alice = await RegisterConfirmed(app, a); var bob = await RegisterConfirmed(app, b);
        var at = await Token(await a.Post("login", new { alice.Email, Password }));
        var bt = await Token(await b.Post("login", new { bob.Email, Password }));
        foreach (var (browser, token, name) in new[] { (a, at, "Alice"), (b, bt, "Bob") })
            await browser.PutApi("profiles/me", new { displayName = name, interestIds = new[] { SampleInterests[0] } }, token);
        var post = await (await a.WriteApi(HttpMethod.Post, "posts", new { content = "Discussion" }, at)).Content.ReadFromJsonAsync<JsonElement>();
        var path = $"posts/{post.GetProperty("id").GetGuid()}/comments";
        var comment = await (await b.WriteApi(HttpMethod.Post, path, new { content = "Root" }, bt)).Content.ReadFromJsonAsync<JsonElement>();
        var commentId = comment.GetProperty("id").GetGuid();
        await a.WriteApi(HttpMethod.Post, path, new { content = "Reply", parentId = commentId }, at);
        Assert.Equal(HttpStatusCode.OK, (await a.WriteApi(HttpMethod.Patch, "notifications/read-all", null, at)).StatusCode);
        var ac = await (await a.GetApi("notifications/unread-count", at)).Content.ReadFromJsonAsync<JsonElement>();
        var bc = await (await b.GetApi("notifications/unread-count", bt)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, ac.GetProperty("count").GetInt32());
        Assert.True(bc.GetProperty("count").GetInt32() > 0);
        Assert.Equal(HttpStatusCode.OK, (await a.WriteApi(HttpMethod.Patch, "notifications/read-all", null, at)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.WriteApi(HttpMethod.Delete, path + "/" + commentId, null, at)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await b.WriteApi(HttpMethod.Delete, path + "/" + commentId, null, bt)).StatusCode);
        var comments = await (await a.GetApi(path, at)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(comments.GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetApi(path + "?parentId=" + commentId, at)).StatusCode);
    }
}
