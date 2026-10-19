using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace DatingApp.Api.Modules.Auth.Services;
[AttributeUsage(AttributeTargets.Method)]
public sealed class ValidateAuthCsrfAttribute() : TypeFilterAttribute(typeof(AuthCsrfFilter));
public sealed class AuthCsrfFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        try { await antiforgery.ValidateRequestAsync(context.HttpContext); }
        catch (AntiforgeryValidationException)
        {
            context.Result = new BadRequestObjectResult(new ProblemDetails
            {
                Status = 400, Title = "CSRF token không hợp lệ.", Detail = "Gọi GET /api/v1/auth/csrf và gửi X-CSRF-TOKEN cùng cookie.",
                Extensions = { ["traceId"] = context.HttpContext.TraceIdentifier }
            });
        }
    }
}
