using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ImageMagick;
using DatingApp.Api.Modules.Profiles.Services;
using Xunit;

namespace DatingApp.Api.Tests;

public sealed partial class AuthFlowTests
{
    [PostgresFact]
    public async Task PhotoApiValidatesImagesEnforcesOwnershipAndCapsConcurrentUploads()
    {
        await using var app = new AuthApp();
        using var owner = app.Browser();
        using var other = app.Browser();
        var ownerAccount = await RegisterConfirmedAt(app, owner, new DateOnly(2000, 1, 1));
        var otherAccount = await RegisterConfirmedAt(app, other, new DateOnly(1999, 1, 1));
        var ownerToken = await Token(await owner.Post("login", new { ownerAccount.Email, Password }));
        var otherToken = await Token(await other.Post("login", new { otherAccount.Email, Password }));

        Assert.Equal(HttpStatusCode.OK, (await owner.PutApi("profiles/me", new
        {
            displayName = "Photo owner", city = "Hà Nội", isDiscoverable = true,
            interestIds = new[] { SampleInterests[0] }
        }, ownerToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await other.PutApi("profiles/me", new
        {
            displayName = "Other user", city = "Đà Nẵng", isDiscoverable = true,
            interestIds = new[] { SampleInterests[1] }
        }, otherToken)).StatusCode);

        var png = CreatePng();
        var first = await owner.UploadProfilePhoto(png, "image/png", ownerToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstPhotoId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.UploadProfilePhoto("not an image"u8.ToArray(), "image/png", ownerToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.UploadProfilePhoto(new byte[5 * 1024 * 1024 + 1], "image/png", ownerToken)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound,
            (await other.ProfileWrite(HttpMethod.Delete, $"photos/{firstPhotoId}", otherToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await other.ProfileWrite(HttpMethod.Put, $"photos/{firstPhotoId}/avatar", otherToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetApi($"photos/{firstPhotoId}/content", ownerToken)).StatusCode);

        for (var i = 1; i < PhotoService.MaxPhotos - 1; i++)
            Assert.Equal(HttpStatusCode.Created, (await owner.UploadProfilePhoto(png, "image/png", ownerToken)).StatusCode);

        var concurrent = await Task.WhenAll(
            owner.UploadProfilePhoto(png, "image/png", ownerToken),
            owner.UploadProfilePhoto(png, "image/png", ownerToken));
        Assert.Single(concurrent, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(concurrent, response => response.StatusCode == HttpStatusCode.BadRequest);

        var profile = await owner.GetApi("profiles/me", ownerToken);
        var profileJson = await profile.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(PhotoService.MaxPhotos, profileJson.GetProperty("photos").GetArrayLength());
    }

    private static byte[] CreatePng()
    {
        using var image = new MagickImage(MagickColors.CadetBlue, 2, 2);
        image.Format = MagickFormat.Png;
        return image.ToByteArray();
    }

    private sealed partial class Browser
    {
        public async Task<HttpResponseMessage> UploadProfilePhoto(byte[] bytes, string contentType, string bearer)
        {
            var csrfResponse = await Get("csrf");
            Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
            var csrf = (await csrfResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
            using var body = new MultipartFormDataContent();
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            body.Add(file, "file", "profile.png");
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/photos") { Content = body };
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearer);
            request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrf);
            if (Cookies.Count > 0)
                request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", Cookies.Select(x => x.Key + "=" + x.Value)));
            return await client.SendAsync(request);
        }

        public async Task<HttpResponseMessage> ProfileWrite(HttpMethod method, string path, string bearer)
        {
            var csrfResponse = await Get("csrf");
            Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
            var csrf = (await csrfResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
            using var request = new HttpRequestMessage(method, "/api/v1/" + path);
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearer);
            request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrf);
            if (Cookies.Count > 0)
                request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", Cookies.Select(x => x.Key + "=" + x.Value)));
            return await client.SendAsync(request);
        }
    }
}
