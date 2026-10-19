using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using DatingApp.Api.Modules.Auth.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DatingApp.Api.Tests;

public sealed class AuthEndpointTests
{
    private const string TestKey = "test-only-signing-key-at-least-32-bytes-long";

    [Theory]
    [InlineData("valid", HttpStatusCode.OK)]
    [InlineData("missing", HttpStatusCode.Unauthorized)]
    [InlineData("expired", HttpStatusCode.Unauthorized)]
    [InlineData("audience", HttpStatusCode.Unauthorized)]
    [InlineData("issuer", HttpStatusCode.Unauthorized)]
    [InlineData("signature", HttpStatusCode.Unauthorized)]
    public async Task MeValidatesBearerToken(string scenario, HttpStatusCode expected)
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseEnvironment("Testing")
                .UseSetting("Auth:FrontendUrl", "https://frontend.test")
                .UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Database=unused;Username=unused")
                .UseSetting("Jwt:Key", TestKey)
                .UseSetting("Jwt:Issuer", "test-issuer")
                .UseSetting("Jwt:Audience", "test-audience")
                .UseSetting("Jwt:ExpireMinutes", "60")
                .ConfigureServices(services =>
                {
                    services.RemoveAll<IAccountAccess>();
                    services.AddScoped<IAccountAccess, TokenOnlyAccess>();
                }));
        using var client = factory.CreateClient();
        var userId = Guid.NewGuid().ToString();
        if (scenario != "missing")
        {
            var token = new JwtSecurityToken(
                issuer: scenario == "issuer" ? "wrong" : "test-issuer",
                audience: scenario == "audience" ? "wrong" : "test-audience",
                claims: [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Email, "test@example.com")],
                expires: scenario == "expired" ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                    scenario == "signature" ? TestKey + "wrong" : TestKey)), SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                new JwtSecurityTokenHandler().WriteToken(token));
        }
        var response = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains(userId, body);
            Assert.Contains("test@example.com", body);
        }
    }

    // Kiểm thử chữ ký JWT riêng; AuthFlowTests kiểm tra phiên PostgreSQL thật.
    private sealed class TokenOnlyAccess : IAccountAccess
    {
        public Task<bool> ValidateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> SetActiveAsync(Guid userId, bool active, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
