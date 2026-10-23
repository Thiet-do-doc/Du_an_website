using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace DatingApp.Api.Tests;

public sealed class DiscoveryMatchingAuthorizationTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("GET", "/api/v1/discover")]
    [InlineData("POST", "/api/v1/user-likes/89f6f148-f81c-4d0c-8a20-0f2f905825bd")]
    [InlineData("POST", "/api/v1/passes/89f6f148-f81c-4d0c-8a20-0f2f905825bd")]
    [InlineData("GET", "/api/v1/matches")]
    [InlineData("DELETE", "/api/v1/matches/89f6f148-f81c-4d0c-8a20-0f2f905825bd")]
    public async Task Anonymous_requests_are_rejected_before_services_are_resolved(string method, string path)
    {
        using var client = factory.CreateClient();
        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
