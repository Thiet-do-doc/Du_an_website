using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
namespace DatingApp.Api.Modules.Auth.Services;
// Đăng ký toàn cục; TV4 vẫn phải kiểm tra quyền match/block trong từng nghiệp vụ chat.
public sealed class AccountHubFilter(IAccountAccess access, AuthConnectionRegistry connections) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext context, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        if (context.Context.User is null || !await access.ValidateAsync(context.Context.User, context.Context.ConnectionAborted))
        {
            context.Context.Abort();
            throw new HubException("Phiên đăng nhập không còn hợp lệ.");
        }
        return await next(context);
    }
    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        if (context.Context.User is null || !await access.ValidateAsync(context.Context.User, context.Context.ConnectionAborted))
        { context.Context.Abort(); throw new HubException("Không được phép kết nối."); }
        var id = Guid.Parse(context.Context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var sessionId = Guid.TryParse(context.Context.User.FindFirstValue("sid"), out var sid) ? sid : (Guid?)null;
        connections.Add(context.Context.ConnectionId, id, context.Context.Abort, sessionId);
        if (!await access.ValidateAsync(context.Context.User, context.Context.ConnectionAborted))
        { connections.Remove(context.Context.ConnectionId); context.Context.Abort(); throw new HubException("Phiên đã bị thu hồi."); }
        try { await next(context); } catch { connections.Remove(context.Context.ConnectionId); throw; }
    }
    public async Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next)
    {
        connections.Remove(context.Context.ConnectionId);
        await next(context, exception);
    }
}
