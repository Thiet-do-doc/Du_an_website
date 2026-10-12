using System.Net;
using System.Net.Http.Json;
using DatingApp.Api.Modules.SystemHealth.DTOs;
using DatingApp.Api.Modules.SystemHealth.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace DatingApp.Api.Tests;

// These tests validate HTTP contracts with a controlled DB probe.
// They do not claim to test an actual PostgreSQL connection.
public sealed class HealthEndpointTests
{
    [Fact]
    public async Task LivenessDoesNotDependOnDatabase()
    {
        await using var factory = CreateFactory(false, "unavailable");
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("ok", body?.Status);
    }

    [Theory]
    [InlineData(true, "connected", HttpStatusCode.OK)]
    [InlineData(false, "not_configured", HttpStatusCode.ServiceUnavailable)]
    [InlineData(false, "unavailable", HttpStatusCode.ServiceUnavailable)]
    public async Task ReadinessReportsActualProbeState(bool ready, string status, HttpStatusCode expected)
    {
        await using var factory = CreateFactory(ready, status);
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/health/database");
        Assert.Equal(expected, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal(status, body?.Status);
        Assert.NotNull(body);
    }

    private static WebApplicationFactory<Program> CreateFactory(bool ready, string status) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDatabaseProbe>();
                services.AddSingleton<IDatabaseProbe>(new StubProbe(ready, status));
            });
        });

    private sealed class StubProbe(bool ready, string status) : IDatabaseProbe
    {
        public Task<DatabaseProbeResult> CheckAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new DatabaseProbeResult(ready, status, "Test probe"));
    }
}
