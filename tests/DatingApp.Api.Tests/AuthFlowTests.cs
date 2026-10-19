using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Collections.Concurrent;
using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.Auth.Entities;
using DatingApp.Api.Modules.Auth.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
namespace DatingApp.Api.Tests;
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AUTH_TEST_CONNECTION")))
            Skip = "Đặt AUTH_TEST_CONNECTION để kiểm thử PostgreSQL thật (database local/test đã áp dụng migration).";
    }
}
public sealed class AuthFlowTests
{
    private const string Password = "DatingTest!2026";
    [PostgresFact]
    public async Task EmailTransportFailureCanRecoverWithResend()
    {
        await using var app = new AuthApp(); using var browser = app.Browser();
        var email = "mail-failure-" + Guid.NewGuid().ToString("N") + "@example.com";
        app.Mail.Fails = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await browser.Post("register", new
            { email, Password, confirmPassword = Password, dateOfBirth = "2000-01-01" })).StatusCode);
        app.Mail.Fails = false;
        Assert.Equal(HttpStatusCode.OK, (await browser.Post("resend-confirmation", new { email })).StatusCode);
        var query = QueryHelpers.ParseQuery(new Uri(app.Mail.Links.Last()).Query);
        Assert.Equal(HttpStatusCode.OK, (await browser.Post("confirm-email", new
            { userId = query["userId"].ToString(), token = query["token"].ToString() })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.Post("login", new { email, Password })).StatusCode);
    }
    [PostgresFact]
    public async Task AuthEndpointsLimitRequestRate()
    {
        await using var app = new AuthApp(); using var browser = app.Browser();
        for (var i = 0; i < 30; i++)
            Assert.Equal(HttpStatusCode.BadRequest, (await browser.Post("login", new { email = "test@example.com", Password }, csrf: false)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await browser.Post("login", new { email = "test@example.com", Password }, csrf: false)).StatusCode);
    }
    [PostgresFact]
    public async Task RegisterConfirmLoginRotateLogoutAndReplay()
    {
        await using var app = new AuthApp(); using var browser = app.Browser();
        var account = await Register(app, browser);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.Post("login", new { account.Email, Password })).StatusCode);
        var confirmation = app.Mail.Links.Last();
        var query = QueryHelpers.ParseQuery(new Uri(confirmation).Query);
        var confirm = new { userId = query["userId"].ToString(), token = query["token"].ToString() };
        Assert.Equal(HttpStatusCode.OK, (await browser.Post("confirm-email", confirm)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.Post("confirm-email", confirm)).StatusCode);
        var login = await browser.Post("login", new { account.Email, Password }); Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var access = await Token(login);
        var originalCookie = browser.Cookies["datingapp.refresh"];
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(x => x.Id == account.Id);
            Assert.NotEqual(Password, user.PasswordHash);
            Assert.True(new PasswordHasher<ApplicationUser>().VerifyHashedPassword(user, user.PasswordHash!, Password) != PasswordVerificationResult.Failed);
            var stored = await db.RefreshTokens.SingleAsync(x => x.UserId == account.Id);
            Assert.Equal(AuthService.Hash(originalCookie), stored.TokenHash); Assert.NotEqual(originalCookie, stored.TokenHash);
        }
        Assert.Equal(HttpStatusCode.OK, (await browser.Get("me", access)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.Get("admin-check", access)).StatusCode);
        var rotated = await browser.Post("refresh"); Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var freshAccess = await Token(rotated);
        Assert.NotEqual(originalCookie, browser.Cookies["datingapp.refresh"]);
        using var replay = app.Browser(); replay.Cookies["datingapp.refresh"] = originalCookie;
        Assert.Equal(HttpStatusCode.Unauthorized, (await replay.Post("refresh")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.Get("me", freshAccess)).StatusCode);
        var again = await browser.Post("login", new { account.Email, Password }); Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var beforeLogout = await Token(again);
        Assert.Equal(HttpStatusCode.NoContent, (await browser.Post("logout")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.Get("me", beforeLogout)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.Post("refresh")).StatusCode);
    }
    [PostgresFact]
    public async Task PasswordResetIsSingleUseAndRevokesExistingAccess()
    {
        await using var app = new AuthApp(); using var browser = app.Browser(); var account = await RegisterConfirmed(app, browser);
        var access = await Token(await browser.Post("login", new { account.Email, Password }));
        Assert.Equal(HttpStatusCode.OK, (await browser.Post("forgot-password", new { account.Email })).StatusCode);
        var query = QueryHelpers.ParseQuery(new Uri(app.Mail.Links.Last()).Query);
        var reset = new { account.Email, token = query["token"].ToString(), password = "NewDating!2026", confirmPassword = "NewDating!2026" };
        Assert.Equal(HttpStatusCode.OK, (await browser.Post("reset-password", reset)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.Post("reset-password", reset)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.Get("me", access)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.Post("login", new { account.Email, Password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.Post("login", new { account.Email, password = "NewDating!2026" })).StatusCode);
    }
    [PostgresFact]
    public async Task LockedAccountAndRolesAreCheckedForExistingToken()
    {
        await using var app = new AuthApp(); using var browser = app.Browser(); var account = await RegisterConfirmed(app, browser);
        var access = await Token(await browser.Post("login", new { account.Email, Password }));
        using (var scope = app.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            if (!await roles.RoleExistsAsync("Admin")) Assert.True((await roles.CreateAsync(new IdentityRole<Guid>("Admin"))).Succeeded);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(account.Id.ToString()))!;
            Assert.True((await users.AddToRoleAsync(user, "Admin")).Succeeded);
        }
        Assert.Equal(HttpStatusCode.OK, (await browser.Get("admin-check", access)).StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.RemoveFromRoleAsync((await users.FindByIdAsync(account.Id.ToString()))!, "Admin")).Succeeded);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.Get("admin-check", access)).StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<IAccountAccess>().SetActiveAsync(account.Id, false));
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.Get("me", access)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.Post("refresh")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.Post("login", new { account.Email, Password })).StatusCode);
    }
    [PostgresFact]
    public async Task CsrfValidationAgeDuplicateAndPasswordLockout()
    {
        await using var app = new AuthApp(); using var browser = app.Browser(); var account = await RegisterConfirmed(app, browser);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.Post("login", new { account.Email, Password }, csrf: false)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.Post("refresh", csrf: false)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.Post("register", new { account.Email, Password, confirmPassword = Password, dateOfBirth = "2000-01-01" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.Post("register", new { email = Guid.NewGuid() + "@example.com", Password, confirmPassword = Password, dateOfBirth = "2015-01-01" })).StatusCode);
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await browser.Post("login", new { account.Email, password = "wrong" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.Post("login", new { account.Email, Password })).StatusCode);
        using var scope = app.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.SingleAsync(x => x.Id == account.Id);
        Assert.True(user.LockoutEnd > DateTimeOffset.UtcNow);
    }
    [PostgresFact]
    public async Task ConcurrentRefreshHasOneWinnerAndRevokesOnReuse()
    {
        await using var app = new AuthApp(); using var browser = app.Browser(); var account = await RegisterConfirmed(app, browser);
        await browser.Post("login", new { account.Email, Password });
        using var other = app.Browser(); other.Cookies["datingapp.refresh"] = browser.Cookies["datingapp.refresh"];
        var results = await Task.WhenAll(browser.Post("refresh"), other.Post("refresh"));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Unauthorized);
        var access = await Token(results.Single(x => x.StatusCode == HttpStatusCode.OK));
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.Get("me", access)).StatusCode);
    }
    [PostgresFact]
    public async Task ExpiredConfirmationResetAndRefreshAreRejected()
    {
        await using var app = new AuthApp(shortEmailLifetime: true); using var browser = app.Browser(); var account = await Register(app, browser);
        var query = QueryHelpers.ParseQuery(new Uri(app.Mail.Links.Last()).Query);
        await Task.Delay(2100);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.Post("confirm-email", new { userId = account.Id, token = query["token"].ToString() })).StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(account.Id.ToString()))!; user.EmailConfirmed = true;
            Assert.True((await users.UpdateAsync(user)).Succeeded);
        }
        await browser.Post("forgot-password", new { account.Email });
        var resetQuery = QueryHelpers.ParseQuery(new Uri(app.Mail.Links.Last()).Query);
        await Task.Delay(2100);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.Post("reset-password", new { account.Email, token = resetQuery["token"].ToString(), Password, confirmPassword = Password })).StatusCode);
        await browser.Post("login", new { account.Email, Password });
        using (var scope = app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().RefreshTokens.Where(x => x.UserId == account.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAtUtc, DateTimeOffset.UtcNow.AddMinutes(-1)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.Post("refresh")).StatusCode);
    }
    private static async Task<(Guid Id, string Email)> Register(AuthApp app, Browser browser)
    {
        var email = "flow-" + Guid.NewGuid().ToString("N") + "@example.com";
        var response = await browser.Post("register", new { email, Password, confirmPassword = Password, dateOfBirth = "2000-01-01" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("userId").GetGuid(), email);
    }
    private static async Task<(Guid Id, string Email)> RegisterConfirmed(AuthApp app, Browser browser)
    {
        var account = await Register(app, browser);
        var query = QueryHelpers.ParseQuery(new Uri(app.Mail.Links.Last()).Query);
        Assert.Equal(HttpStatusCode.OK, (await browser.Post("confirm-email", new { userId = account.Id, token = query["token"].ToString() })).StatusCode);
        return account;
    }
    private static async Task<string> Token(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    }
    private sealed class CaptureMail : IAuthEmailSender
    {
        public bool Fails { get; set; }
        public ConcurrentQueue<string> Links { get; } = new();
        public Task SendAsync(string email, string subject, string link, CancellationToken cancellationToken = default)
        { if (Fails) throw new AuthEmailDeliveryException(new System.Net.Mail.SmtpException("Test transport failure"));
            Links.Enqueue(link); return Task.CompletedTask; }
    }
    private sealed class AuthApp : WebApplicationFactory<Program>
    {
        public CaptureMail Mail { get; } = new();
        private readonly bool _shortEmailLifetime;
        public AuthApp(bool shortEmailLifetime = false) => _shortEmailLifetime = shortEmailLifetime;
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing")
                .UseSetting("ConnectionStrings:DefaultConnection", Environment.GetEnvironmentVariable("AUTH_TEST_CONNECTION"))
                .UseSetting("Jwt:Key", "integration-test-key-at-least-32-bytes-long")
                .UseSetting("Jwt:Issuer", "test-issuer").UseSetting("Jwt:Audience", "test-audience")
                .UseSetting("Jwt:ExpireMinutes", "10").UseSetting("Auth:FrontendUrl", "https://frontend.test");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAuthEmailSender>(); services.AddSingleton<IAuthEmailSender>(Mail);
                if (_shortEmailLifetime) services.PostConfigure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromSeconds(1));
            });
        }
        public Browser Browser() => new(CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, BaseAddress = new Uri("https://localhost") }));
    }
    private sealed class Browser(HttpClient client) : IDisposable
    {
        public Dictionary<string, string> Cookies { get; } = new();
        public async Task<HttpResponseMessage> Get(string path, string? bearer = null) => await Send(HttpMethod.Get, path, null, bearer, null);
        public async Task<HttpResponseMessage> Post(string path, object? body = null, bool csrf = true)
        {
            string? token = null;
            if (csrf)
            {
                var response = await Get("csrf"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
            }
            return await Send(HttpMethod.Post, path, body ?? new { }, null, token);
        }
        private async Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body, string? bearer, string? csrf)
        {
            using var request = new HttpRequestMessage(method, "/api/v1/auth/" + path);
            if (body is not null) request.Content = JsonContent.Create(body);
            if (bearer is not null) request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearer);
            if (csrf is not null) request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrf);
            if (Cookies.Count > 0) request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", Cookies.Select(x => x.Key + "=" + x.Value)));
            var response = await client.SendAsync(request);
            if (response.Headers.TryGetValues("Set-Cookie", out var values))
                foreach (var value in values)
                { var pair = value.Split(';')[0].Split('=', 2); if (pair[1].Length == 0) Cookies.Remove(pair[0]); else Cookies[pair[0]] = pair[1]; }
            return response;
        }
        public void Dispose() => client.Dispose();
    }
}
