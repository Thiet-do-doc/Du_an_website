using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DatingApp.Api.Tests;

public sealed partial class AuthFlowTests
{
    [PostgresFact]
    public async Task RepliesReactionsAndNotificationsRespectOwnershipAndBlocks()
    {
        await using var app = new AuthApp();
        using var a = app.Browser(); using var b = app.Browser(); using var c = app.Browser();
        var alice = await RegisterConfirmed(app, a); var bob = await RegisterConfirmed(app, b); var carol = await RegisterConfirmed(app, c);
        var at = await Token(await a.Post("login", new { alice.Email, Password }));
        var bt = await Token(await b.Post("login", new { bob.Email, Password }));
        var ct = await Token(await c.Post("login", new { carol.Email, Password }));
        foreach (var (browser, token, name) in new[] { (a, at, "Alice"), (b, bt, "Bob"), (c, ct, "Carol") })
            Assert.Equal(HttpStatusCode.OK, (await browser.PutApi("profiles/me", new { displayName = name, interestIds = new[] { SampleInterests[0] } }, token)).StatusCode);
        var post = await (await a.WriteApi(HttpMethod.Post, "posts", new { content = "Conversation test" }, at)).Content.ReadFromJsonAsync<JsonElement>();
        var postId = post.GetProperty("id").GetGuid();
        var path = $"posts/{postId}/comments";
        var comment = await (await b.WriteApi(HttpMethod.Post, path, new { content = "A comment" }, bt)).Content.ReadFromJsonAsync<JsonElement>();
        var commentId = comment.GetProperty("id").GetGuid();
        var reply = await a.WriteApi(HttpMethod.Post, path, new { content = "A reply", parentId = commentId }, at);
        Assert.Equal(HttpStatusCode.Created, reply.StatusCode);
        var roots = await (await a.GetApi(path, at)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, Assert.Single(roots.GetProperty("items").EnumerateArray()).GetProperty("replyCount").GetInt32());
        var replies = await (await a.GetApi(path + $"?parentId={commentId}", at)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("A reply", Assert.Single(replies.GetProperty("items").EnumerateArray()).GetProperty("content").GetString());
        var otherPost = await (await a.WriteApi(HttpMethod.Post, "posts", new { content = "Another post" }, at)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.NotFound, (await a.WriteApi(HttpMethod.Post, $"posts/{otherPost.GetProperty("id").GetGuid()}/comments", new { content = "Wrong parent", parentId = commentId }, at)).StatusCode);
        var reactionPath = path + $"/{commentId}/reaction";
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PutApi(reactionPath, new { kind = "invalid" }, at)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a.PutApi(reactionPath, new { kind = "like" }, at)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a.PutApi(reactionPath, new { kind = "haha" }, at)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.PutApi(reactionPath, new { kind = "sad" }, ct)).StatusCode);
        roots = await (await a.GetApi(path, at)).Content.ReadFromJsonAsync<JsonElement>();
        var root = Assert.Single(roots.GetProperty("items").EnumerateArray());
        Assert.Equal("haha", root.GetProperty("myReaction").GetString());
        Assert.Equal(0, root.GetProperty("reactions").GetProperty("like").GetInt32());
        Assert.Equal(1, root.GetProperty("reactions").GetProperty("haha").GetInt32());
        Assert.Equal(1, root.GetProperty("reactions").GetProperty("sad").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, (await a.WriteApi(HttpMethod.Delete, reactionPath, null, at)).StatusCode);

        var notices = await (await b.GetApi("notifications", bt)).Content.ReadFromJsonAsync<JsonElement>();
        var notice = Assert.Single(notices.GetProperty("items").EnumerateArray());
        Assert.Equal("reply", notice.GetProperty("type").GetString());
        Assert.Equal(postId, notice.GetProperty("targetId").GetGuid());
        var noticeId = notice.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await c.WriteApi(HttpMethod.Patch, $"notifications/{noticeId}/read", null, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await b.WriteApi(HttpMethod.Patch, $"notifications/{noticeId}/read", null, bt)).StatusCode);
        var count = await (await b.GetApi("notifications/unread-count", bt)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, count.GetProperty("count").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await a.WriteApi(HttpMethod.Post, $"blocks/{bob.Id}", new { }, at)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.PutApi(reactionPath, new { kind = "angry" }, at)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.WriteApi(HttpMethod.Post, path, new { content = "Blocked reply", parentId = commentId }, at)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetApi(path + $"?parentId={commentId}", at)).StatusCode);
        notices = await (await b.GetApi("notifications", bt)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(notices.GetProperty("items").EnumerateArray());
    }

    [PostgresFact]
    public async Task SocialNotificationsAndPersonalReportListAreScopedToRecipient()
    {
        await using var app = new AuthApp();
        using var a = app.Browser(); using var b = app.Browser();
        var alice = await RegisterConfirmed(app, a); var bob = await RegisterConfirmed(app, b);
        var at = await Token(await a.Post("login", new { alice.Email, Password }));
        var bt = await Token(await b.Post("login", new { bob.Email, Password }));
        foreach (var (browser, token, name) in new[] { (a, at, "Alice"), (b, bt, "Bob") })
            await browser.PutApi("profiles/me", new { displayName = name, isDiscoverable = true, interestIds = new[] { SampleInterests[0] } }, token);
        await a.WriteApi(HttpMethod.Post, $"user-likes/{bob.Id}", new { }, at);
        await a.WriteApi(HttpMethod.Post, $"user-likes/{bob.Id}", new { }, at);
        var notices = await (await b.GetApi("notifications", bt)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("profileLike", Assert.Single(notices.GetProperty("items").EnumerateArray()).GetProperty("type").GetString());
        await b.WriteApi(HttpMethod.Post, $"user-likes/{alice.Id}", new { }, bt);
        var post = await (await a.WriteApi(HttpMethod.Post, "posts", new { content = "New post for a friend" }, at)).Content.ReadFromJsonAsync<JsonElement>();
        var id = post.GetProperty("id").GetGuid();
        notices = await (await b.GetApi("notifications", bt)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(notices.GetProperty("items").EnumerateArray(), n => n.GetProperty("type").GetString() == "friendPost" && n.GetProperty("targetId").GetGuid() == id);
        await b.WriteApi(HttpMethod.Put, $"posts/{id}/like", null, bt);
        await b.WriteApi(HttpMethod.Put, $"posts/{id}/like", null, bt);
        notices = await (await a.GetApi("notifications", at)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(notices.GetProperty("items").EnumerateArray(), n => n.GetProperty("type").GetString() == "postLike");
        var report = await a.WriteApi(HttpMethod.Post, "reports", new { targetType = "User", targetId = bob.Id, reason = "Test", description = "Report visibility test" }, at);
        Assert.Equal(HttpStatusCode.Created, report.StatusCode);
        var reports = await (await a.GetApi("reports/mine", at)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(reports.GetProperty("items").EnumerateArray());
        reports = await (await b.GetApi("reports/mine", bt)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(reports.GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await b.GetApi("admin/reports", bt)).StatusCode);
        await a.WriteApi(HttpMethod.Delete, $"posts/{id}", null, at);
        notices = await (await b.GetApi("notifications", bt)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain(notices.GetProperty("items").EnumerateArray(), n => n.GetProperty("type").GetString() == "friendPost");
    }
}
