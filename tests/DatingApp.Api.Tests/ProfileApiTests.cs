using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DatingApp.Api.Modules.Profiles.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DatingApp.Api.Tests;

public sealed partial class AuthFlowTests
{
    private static readonly Guid[] SampleInterests =
    [
        Guid.Parse("10000000-0000-0000-0000-000000000001"),
        Guid.Parse("10000000-0000-0000-0000-000000000002"),
        Guid.Parse("10000000-0000-0000-0000-000000000003")
    ];

    [PostgresFact]
    public async Task ProfileApiPersistsOwnerDataAndReturnsPrivateSafeContracts()
    {
        await using var app = new AuthApp();
        using var owner = app.Browser();
        using var other = app.Browser();
        using var third = app.Browser();
        var ownerAccount = await RegisterConfirmedAt(app, owner, new DateOnly(2000, 1, 1));
        var otherAccount = await RegisterConfirmedAt(app, other, new DateOnly(1999, 1, 1));
        var thirdAccount = await RegisterConfirmedAt(app, third, new DateOnly(1986, 1, 1));
        var ownerToken = await Token(await owner.Post("login", new { ownerAccount.Email, Password }));
        var otherToken = await Token(await other.Post("login", new { otherAccount.Email, Password }));
        var thirdToken = await Token(await third.Post("login", new { thirdAccount.Email, Password }));

        var save = await owner.PutApi("profiles/me", new
        {
            displayName = "Linh", bio = "Thích đọc sách", city = "Hà Nội", gender = "Nữ",
            isDiscoverable = true, interestIds = new[] { SampleInterests[0], SampleInterests[1] }
        }, ownerToken);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var reload = await owner.GetApi("profiles/me", ownerToken);
        Assert.Equal(HttpStatusCode.OK, reload.StatusCode);
        var privateJson = await reload.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Linh", privateJson.GetProperty("displayName").GetString());
        Assert.Equal("Hà Nội", privateJson.GetProperty("city").GetString());
        Assert.Equal(2, privateJson.GetProperty("interests").GetArrayLength());
        Assert.Equal(ownerAccount.Id, privateJson.GetProperty("userId").GetGuid());

        var updateAgain = await owner.PutApi("profiles/me", new
        {
            displayName = "Linh", bio = "Cập nhật lần hai", city = "Hà Nội", gender = "Nữ",
            isDiscoverable = true, interestIds = new[] { SampleInterests[1], SampleInterests[2] }
        }, ownerToken);
        Assert.Equal(HttpStatusCode.OK, updateAgain.StatusCode);
        var afterSecondSave = await owner.GetApi("profiles/me", ownerToken);
        var secondSaveJson = await afterSecondSave.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Cập nhật lần hai", secondSaveJson.GetProperty("bio").GetString());
        Assert.Equal(2, secondSaveJson.GetProperty("interests").GetArrayLength());

        // A forged userId in the payload cannot redirect an owner-scoped write.
        var ownershipAttempt = await owner.PutApi("profiles/me", new
        {
            userId = otherAccount.Id, displayName = "Không được sửa hồ sơ B", bio = "", city = "Hà Nội",
            gender = "Nữ", isDiscoverable = true, interestIds = new[] { SampleInterests[0] }
        }, ownerToken);
        Assert.Equal(HttpStatusCode.OK, ownershipAttempt.StatusCode);
        Assert.Equal(ownerAccount.Id,
            (await ownershipAttempt.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("userId").GetGuid());
        var otherBeforeEdit = await other.GetApi("profiles/me", otherToken);
        Assert.Equal("", (await otherBeforeEdit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("displayName").GetString());
        var publicResponse = await owner.GetApi($"profiles/{ownerAccount.Id}", ownerToken);
        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
        var publicJson = await publicResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(publicJson.TryGetProperty("email", out _));
        Assert.False(publicJson.TryGetProperty("dateOfBirth", out _));
        Assert.False(publicJson.TryGetProperty("isDiscoverable", out _));
        Assert.False(publicJson.TryGetProperty("isComplete", out _));

        var invalidDuplicates = await owner.PutApi("profiles/me", new
        {
            displayName = "Linh", bio = (string?)null, city = "Hà Nội", gender = "Nữ",
            isDiscoverable = true, interestIds = new[] { SampleInterests[0], SampleInterests[0] }
        }, ownerToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidDuplicates.StatusCode);
        var unchanged = await owner.GetApi("profiles/me", ownerToken);
        Assert.Equal(2, (await unchanged.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("interests").GetArrayLength());

        // Incomplete profiles are not discoverable through the integration projection.
        var incompleteSave = await other.PutApi("profiles/me", new
        {
            displayName = "Minh", bio = "", city = "Đà Nẵng", gender = "Nam",
            isDiscoverable = true, interestIds = Array.Empty<Guid>()
        }, otherToken);
        Assert.Equal(HttpStatusCode.OK, incompleteSave.StatusCode);
        var thirdSave = await third.PutApi("profiles/me", new
        {
            displayName = "Mai", bio = "Thích âm nhạc", city = "Cần Thơ", gender = "Nữ",
            isDiscoverable = true, interestIds = new[] { SampleInterests[2] }
        }, thirdToken);
        Assert.Equal(HttpStatusCode.OK, thirdSave.StatusCode);
        using var scope = app.Services.CreateScope();
        var query = scope.ServiceProvider.GetRequiredService<IDiscoveryProfileQuery>();
        Assert.NotNull(await query.GetSummaryAsync(ownerAccount.Id));
        Assert.Null(await query.GetSummaryAsync(otherAccount.Id));
        var thirdSummary = await query.GetSummaryAsync(thirdAccount.Id);
        Assert.NotNull(thirdSummary);
        Assert.Equal("Cần Thơ", thirdSummary.City);
        Assert.Single(thirdSummary.Interests);
        Assert.Equal(40, thirdSummary.Age);
    }

    private static async Task<(Guid Id, string Email)> RegisterConfirmedAt(AuthApp app, Browser browser, DateOnly dateOfBirth)
    {
        var email = "profile-" + Guid.NewGuid().ToString("N") + "@example.com";
        var response = await browser.Post("register", new { email, Password, confirmPassword = Password, dateOfBirth });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("userId").GetGuid();
        var query = QueryHelpers.ParseQuery(new Uri(app.Mail.Links.Last()).Query);
        Assert.Equal(HttpStatusCode.OK, (await browser.Post("confirm-email", new
        { userId = id, token = query["token"].ToString() })).StatusCode);
        return (id, email);
    }
}

public sealed partial class AuthFlowTests
{
    private sealed partial class Browser
    {
        public Task<HttpResponseMessage> GetApi(string path, string bearer) => ApiSend(HttpMethod.Get, path, null, bearer, null);

        public async Task<HttpResponseMessage> PutApi(string path, object body, string bearer)
        {
            var csrf = await Get("csrf");
            Assert.Equal(HttpStatusCode.OK, csrf.StatusCode);
            var token = (await csrf.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
            return await ApiSend(HttpMethod.Put, path, body, bearer, token);
        }

        private async Task<HttpResponseMessage> ApiSend(HttpMethod method, string path, object? body,
            string? bearer, string? csrf)
        {
            using var request = new HttpRequestMessage(method, "/api/v1/" + path);
            if (body is not null) request.Content = JsonContent.Create(body);
            if (bearer is not null) request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearer);
            if (csrf is not null) request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrf);
            if (Cookies.Count > 0) request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", Cookies.Select(x => x.Key + "=" + x.Value)));
            return await client.SendAsync(request);
        }
    }
}
