using DatingApp.Api.Modules.Auth.DTOs;
using DatingApp.Api.Modules.Auth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
namespace DatingApp.Api.Modules.Auth.Controllers;
[ApiController]
[Route("api/v1/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(AuthService auth, IAntiforgery antiforgery, IWebHostEnvironment environment) : ControllerBase
{
    private const string CookieName = "datingapp.refresh";
    [HttpGet("csrf")]
    [AllowAnonymous]
    public IActionResult Csrf() => Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });

    [HttpPost("register"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var (result, userId) = await auth.RegisterAsync(request);
        return result.Succeeded ? Ok(new { message = "Đăng ký thành công. Hãy kiểm tra email để xác nhận.", userId }) : IdentityFailure(result);
    }
    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("auth"), ValidateAuthCsrf]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var session = await auth.LoginAsync(request);
        return session is null ? InvalidLogin() : SessionResponse(session);
    }
    [HttpPost("refresh"), AllowAnonymous, EnableRateLimiting("auth"), ValidateAuthCsrf]
    public async Task<IActionResult> Refresh()
    {
        var raw = Request.Cookies[CookieName];
        var session = string.IsNullOrEmpty(raw) ? null : await auth.RefreshAsync(raw);
        if (session is not null) return SessionResponse(session);
        ClearCookie();
        return InvalidLogin();
    }
    [HttpPost("logout"), AllowAnonymous, ValidateAuthCsrf]
    public async Task<IActionResult> Logout()
    {
        await auth.LogoutAsync(Request.Cookies[CookieName]);
        ClearCookie();
        return NoContent();
    }
    [HttpPost("confirm-email"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request)
    {
        var result = await auth.ConfirmEmailAsync(request);
        return result.Succeeded ? Ok(new { message = "Email đã được xác nhận. Bạn có thể đăng nhập." }) : IdentityFailure(result);
    }
    [HttpPost("resend-confirmation"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> Resend(EmailRequest request)
    {
        await auth.SendConfirmationAsync(request.Email);
        return EmailAccepted();
    }
    [HttpPost("forgot-password"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword(EmailRequest request)
    {
        await auth.ForgotPasswordAsync(request.Email);
        return EmailAccepted();
    }
    [HttpPost("reset-password"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var result = await auth.ResetPasswordAsync(request);
        return result.Succeeded ? Ok(new { message = "Đã đổi mật khẩu. Hãy đăng nhập lại." }) : IdentityFailure(result);
    }
    [Authorize(Policy = "User"), HttpGet("me")]
    public IActionResult Me() => Ok(new { authenticated = true, userId = User.FindFirstValue(ClaimTypes.NameIdentifier),
        email = User.FindFirstValue(ClaimTypes.Email), roles = User.FindAll(ClaimTypes.Role).Select(x => x.Value) });
    [Authorize(Policy = "Admin"), HttpGet("admin-check")]
    public IActionResult AdminCheck() => Ok(new { authorized = true });
    private IActionResult EmailAccepted() => Ok(new { message = "Nếu tài khoản phù hợp, email hướng dẫn sẽ được gửi." });
    private IActionResult InvalidLogin() => Problem(statusCode: 401, title: "Đăng nhập không thành công.",
        detail: "Kiểm tra email, mật khẩu, xác nhận email hoặc trạng thái tài khoản.");
    private IActionResult IdentityFailure(IdentityResult result)
    {
        foreach (var error in result.Errors) ModelState.AddModelError(error.Code, error.Description);
        return ValidationProblem(ModelState);
    }
    private IActionResult SessionResponse(AuthSession session)
    {
        Response.Cookies.Append(CookieName, session.RefreshToken, CookieOptions(session.RefreshExpiresAt));
        var result = session.Response;
        return Ok(new { message = "Đăng nhập thành công.", token = result.Token, expiresAt = result.ExpiresAt,
            user = new { id = result.UserId, email = result.Email } });
    }
    private CookieOptions CookieOptions(DateTimeOffset? expires = null) => new()
    {
        HttpOnly = true, Secure = !environment.IsDevelopment(), SameSite = SameSiteMode.Strict,
        Path = "/api/v1/auth", Expires = expires, IsEssential = true
    };
    private void ClearCookie() => Response.Cookies.Delete(CookieName, CookieOptions());
}
