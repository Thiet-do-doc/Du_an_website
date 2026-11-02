using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
namespace DatingApp.Api.Tests;
public sealed partial class AuthFlowTests
{
    [PostgresFact]
    public async Task CommunityFeedRequiresAuthFiltersBlocksAndRestrictsDeletionToAuthor()
    {
        await using var app = new AuthApp();
        using var a = app.Browser(); using var b = app.Browser();
        var alice = await RegisterConfirmed(app, a); var bob = await RegisterConfirmed(app, b);
        var at = await Token(await a.Post("login", new { alice.Email, Password }));
        var bt = await Token(await b.Post("login", new { bob.Email, Password }));
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.GetApi("posts", "")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.WriteApi(HttpMethod.Post, "posts", new { content = "anonymous" }, "")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await a.WriteApi(HttpMethod.Post, "posts", new { content = "no profile yet" }, at)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.PutApi("profiles/me", new { displayName = "Mylove test", isDiscoverable = false, interestIds = Array.Empty<Guid>() }, at)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.WriteApi(HttpMethod.Post, "posts", new { content = "  " }, at)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.WriteApi(HttpMethod.Post, "posts", new { content = new string('a', 2001) }, at)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.GetApi("posts?page=0", at)).StatusCode);
        var created = await a.WriteApi(HttpMethod.Post, "posts", new { content = "  A real community post ♡  ", authorId = bob.Id }, at);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var data = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = data.GetProperty("id").GetGuid();
        Assert.Equal(alice.Id, data.GetProperty("authorId").GetGuid());
        Assert.Equal("A real community post ♡", data.GetProperty("content").GetString());
        Assert.True(data.GetProperty("isMine").GetBoolean());
        var feed = await (await b.GetApi("posts", bt)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(feed.GetProperty("items").EnumerateArray(), p => p.GetProperty("id").GetGuid() == id && !p.GetProperty("isMine").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await b.WriteApi(HttpMethod.Delete, $"posts/{id}", null, bt)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await b.WriteApi(HttpMethod.Post, $"blocks/{alice.Id}", new { }, bt)).StatusCode);
        feed = await (await b.GetApi("posts", bt)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain(feed.GetProperty("items").EnumerateArray(), p => p.GetProperty("id").GetGuid() == id);
        Assert.Equal(HttpStatusCode.NoContent, (await b.WriteApi(HttpMethod.Delete, $"blocks/{alice.Id}", null, bt)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a.WriteApi(HttpMethod.Post, $"blocks/{bob.Id}", new { }, at)).StatusCode);
        feed = await (await b.GetApi("posts", bt)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain(feed.GetProperty("items").EnumerateArray(), p => p.GetProperty("id").GetGuid() == id);
        Assert.Equal(HttpStatusCode.NoContent, (await a.WriteApi(HttpMethod.Delete, $"posts/{id}", null, at)).StatusCode);
        feed = await (await a.GetApi("posts", at)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain(feed.GetProperty("items").EnumerateArray(), p => p.GetProperty("id").GetGuid() == id);
    }
}
