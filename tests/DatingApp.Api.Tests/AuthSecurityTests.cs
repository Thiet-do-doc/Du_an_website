using System.Security.Claims;
using DatingApp.Api.Modules.Auth.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Xunit;
namespace DatingApp.Api.Tests;
public sealed class AuthSecurityTests
{
    [Fact]
    public void CurrentUserUsesAuthenticatedClaims()
    {
        var id = Guid.NewGuid();
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                new Claim(ClaimTypes.Email, "user@example.com"), new Claim(ClaimTypes.Role, "Admin")], "test")) } };
        var current = new CurrentUser(accessor);
        Assert.Equal(id, current.UserId); Assert.Equal("user@example.com", current.Email); Assert.True(current.IsAdmin);
    }
    [Fact]
    public async Task HubChecksAccountOnConnectAndEveryInvocation()
    {
        var access = new StubAccess(); var registry = new AuthConnectionRegistry();
        var filter = new AccountHubFilter(access, registry); var caller = new Caller();
        var hub = new TestHub(); var lifetime = new HubLifetimeContext(caller, new EmptyServices(), hub);
        var connected = false;
        await filter.OnConnectedAsync(lifetime, _ => { connected = true; return Task.CompletedTask; });
        Assert.True(connected); Assert.False(caller.Aborted);
        var invocation = new HubInvocationContext(caller, new EmptyServices(), hub, typeof(TestHub).GetMethod(nameof(TestHub.Ping))!, []);
        Assert.Equal("pong", await filter.InvokeMethodAsync(invocation, _ => ValueTask.FromResult<object?>("pong")));
        access.Allowed = false;
        await Assert.ThrowsAsync<HubException>(async () => await filter.InvokeMethodAsync(invocation, _ => ValueTask.FromResult<object?>("pong")));
        Assert.True(caller.Aborted);
    }
    [Fact]
    public async Task AccountLockDisconnectsOnlyThatUsersConnections()
    {
        var registry = new AuthConnectionRegistry(); var access = new StubAccess(); var filter = new AccountHubFilter(access, registry);
        var a = new Caller(); var b = new Caller();
        await filter.OnConnectedAsync(new HubLifetimeContext(a, new EmptyServices(), new TestHub()), _ => Task.CompletedTask);
        await filter.OnConnectedAsync(new HubLifetimeContext(b, new EmptyServices(), new TestHub()), _ => Task.CompletedTask);
        registry.Disconnect(a.Id);
        Assert.True(a.Aborted); Assert.False(b.Aborted);
        await filter.OnDisconnectedAsync(new HubLifetimeContext(b, new EmptyServices(), new TestHub()), null, (_, _) => Task.CompletedTask);
        registry.Disconnect(b.Id); Assert.False(b.Aborted);
    }
    private sealed class StubAccess : IAccountAccess
    {
        public bool Allowed { get; set; } = true;
        public Task<bool> ValidateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default) => Task.FromResult(Allowed);
        public Task<bool> SetActiveAsync(Guid userId, bool active, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
    private sealed class EmptyServices : IServiceProvider { public object? GetService(Type serviceType) => null; }
    public sealed class TestHub : Hub { public string Ping() => "pong"; }
    private sealed class Caller : HubCallerContext
    {
        public Guid Id { get; } = Guid.NewGuid();
        public bool Aborted { get; private set; }
        public override string ConnectionId { get; } = Guid.NewGuid().ToString();
        public override string? UserIdentifier => Id.ToString();
        public override ClaimsPrincipal User => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Id.ToString())], "test"));
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override Microsoft.AspNetCore.Http.Features.IFeatureCollection Features { get; } = new Microsoft.AspNetCore.Http.Features.FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() => Aborted = true;
    }
}
