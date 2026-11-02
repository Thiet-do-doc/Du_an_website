using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
namespace DatingApp.Api.Tests;
public sealed partial class AuthFlowTests
{
    [PostgresFact]
    public async Task PhotoPostsAndFeelingOnlyPostsKeepMemberVisibility()
    {
        await using var app = new AuthApp();
        using var a = app.Browser(); using var b = app.Browser();
        var alice = await RegisterConfirmed(app, a); var bob = await RegisterConfirmed(app, b);
        var at = await Token(await a.Post("login", new { alice.Email, Password }));
        var bt = await Token(await b.Post("login", new { bob.Email, Password }));
        await a.PutApi("profiles/me", new { displayName = "Linh", interestIds = Array.Empty<Guid>() }, at);
        var feelingOnly = await a.WriteApi(HttpMethod.Post, "posts", new { feeling = "Bình yên" }, at);
        Assert.Equal(HttpStatusCode.Created, feelingOnly.StatusCode);
        var mood = await feelingOnly.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Bình yên", mood.GetProperty("feeling").GetString());
        Assert.False(mood.GetProperty("hasPhoto").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest,
            (await a.WriteApi(HttpMethod.Post, "posts", new { feeling = "unknown" }, at)).StatusCode);
        byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC");
        Assert.Equal(HttpStatusCode.BadRequest, (await a.UploadPng("posts", [1, 2, 3], at)).StatusCode);
        var photoPost = await a.UploadPng("posts", png, at);
        Assert.Equal(HttpStatusCode.Created, photoPost.StatusCode);
        var body = await photoPost.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("hasPhoto").GetBoolean());
        var path = $"posts/{body.GetProperty("id").GetGuid()}/photo";
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.GetApi(path, "")).StatusCode);
        var photo = await b.GetApi(path, bt);
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal("image/png", photo.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.NoContent, (await b.WriteApi(HttpMethod.Post, $"blocks/{alice.Id}", new { }, bt)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetApi(path, bt)).StatusCode);
    }

    private sealed partial class Browser
    {
        public async Task<HttpResponseMessage> UploadPng(string path, byte[] bytes, string token)
        {
            using var form = new MultipartFormDataContent();
            var image = new ByteArrayContent(bytes);
            image.Headers.ContentType = new("image/png");
            form.Add(image, "file", "photo.png");
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/" + path) { Content = form };
            request.Headers.Authorization = new("Bearer", token);
            return await client.SendAsync(request);
        }
    }

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
        var path = $"posts/{id}";
        Assert.Equal(HttpStatusCode.NotFound, (await b.WriteApi(HttpMethod.Delete, path, null, bt)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.GetApi(path, "")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await b.PutApi("profiles/me", new { displayName = "Bob", interestIds = Array.Empty<Guid>() }, bt)).StatusCode);
        foreach (var response in await Task.WhenAll(
            b.WriteApi(HttpMethod.Put, path + "/like", null, bt), b.WriteApi(HttpMethod.Put, path + "/like", null, bt)))
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await b.WriteApi(HttpMethod.Post, path + "/comments", new { content = " " }, bt)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await b.WriteApi(HttpMethod.Post, path + "/comments", new { content = "Hello" }, bt)).StatusCode);
        var detail = await (await b.GetApi(path, bt)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(detail.GetProperty("isLiked").GetBoolean());
        Assert.Equal(1, detail.GetProperty("likeCount").GetInt32());
        Assert.Equal(1, detail.GetProperty("commentCount").GetInt32());
        var comments = await (await a.GetApi(path + "/comments", at)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Hello", Assert.Single(comments.GetProperty("items").EnumerateArray()).GetProperty("content").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await b.WriteApi(HttpMethod.Delete, path + "/like", null, bt)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await b.WriteApi(HttpMethod.Post, $"blocks/{alice.Id}", new { }, bt)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetApi(path, bt)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetApi(path + "/comments", bt)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.WriteApi(HttpMethod.Put, path + "/like", null, bt)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.WriteApi(HttpMethod.Post, path + "/comments", new { content = "hidden" }, bt)).StatusCode);
        comments = await (await a.GetApi(path + "/comments", at)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(comments.GetProperty("items").EnumerateArray());
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
