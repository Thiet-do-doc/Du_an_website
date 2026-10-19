using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
namespace DatingApp.Api.Modules.Auth.Services;
public sealed class AuthEmailDeliveryException(Exception inner) : Exception("Không gửi được email xác nhận/reset.", inner);
public sealed class AuthEmailExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not AuthEmailDeliveryException) return false;
        context.Response.StatusCode = 503;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = 503, Title = "Dịch vụ email tạm thời không khả dụng.",
            Detail = "Bạn có thể thử gửi lại email xác nhận hoặc yêu cầu đặt lại mật khẩu sau.",
            Extensions = { ["traceId"] = context.TraceIdentifier }
        }, cancellationToken);
        return true;
    }
}
