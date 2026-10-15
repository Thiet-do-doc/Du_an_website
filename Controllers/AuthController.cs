using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Vo_T1_2_AuthStarter.Contracts;
using Vo_T1_2_AuthStarter.Models;
using Vo_T1_2_AuthStarter.Services;

namespace Vo_T1_2_AuthStarter.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IEmailSender emailSender,
    IConfiguration configuration,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (!IsAdult(request.DateOfBirth))
            return BadRequest(new { message = "Bạn phải đủ 18 tuổi để đăng ký." });

        var email = request.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
            return Conflict(new { message = "Email này đã được sử dụng." });

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DateOfBirth = request.DateOfBirth,
            IsActive = true
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => new { e.Code, e.Description }) });

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var baseUrl = configuration["App:PublicBaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            return StatusCode(500, new { message = "Thiếu cấu hình App:PublicBaseUrl." });

        var confirmUrl = $"{baseUrl}/api/v1/auth/confirm-email?userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(encodedToken)}";
        await emailSender.SendAsync(email, "Xác nhận email", $"<p>Nhấn để xác nhận email: <a href=\"{confirmUrl}\">Xác nhận email</a></p>");

        return StatusCode(StatusCodes.Status201Created,
            new AuthResponse("Đăng ký thành công. Vui lòng kiểm tra email để xác nhận tài khoản.", true));
    }

    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromQuery] string userId, [FromQuery] string token)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return BadRequest(new { message = "Liên kết xác nhận không hợp lệ." });
        try
        {
            var decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            var result = await userManager.ConfirmEmailAsync(user, decoded);
            if (!result.Succeeded)
                return BadRequest(new { message = "Liên kết xác nhận hết hạn hoặc không hợp lệ." });
            return Ok(new AuthResponse("Xác nhận email thành công. Bạn có thể đăng nhập."));
        }
        catch (FormatException)
        {
            return BadRequest(new { message = "Liên kết xác nhận không hợp lệ." });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        // Same error message avoids disclosing whether an email exists.
        if (user is null || !user.IsActive)
            return Unauthorized(new { message = "Email hoặc mật khẩu không đúng, hoặc tài khoản chưa hoạt động." });
        if (!await userManager.IsEmailConfirmedAsync(user))
            return Unauthorized(new { message = "Vui lòng xác nhận email trước khi đăng nhập." });

        var result = await signInManager.PasswordSignInAsync(user, request.Password, isPersistent: false, lockoutOnFailure: true);
        if (result.Succeeded) return Ok(new AuthResponse("Đăng nhập thành công."));
        if (result.IsLockedOut) return StatusCode(StatusCodes.Status423Locked, new { message = "Tài khoản tạm khóa do đăng nhập sai nhiều lần." });
        return Unauthorized(new { message = "Email hoặc mật khẩu không đúng." });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return Ok(new AuthResponse("Đăng xuất thành công."));
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(EmailRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        // Always return the same response to prevent email enumeration.
        if (user is not null && user.IsActive && await userManager.IsEmailConfirmedAsync(user))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var baseUrl = configuration["App:PublicBaseUrl"]?.TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                var resetUrl = $"{baseUrl}/reset-password?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(encodedToken)}";
                await emailSender.SendAsync(user.Email!, "Đặt lại mật khẩu", $"<p>Liên kết đặt lại mật khẩu: <a href=\"{resetUrl}\">Đặt lại mật khẩu</a></p>");
            }
        }
        return Ok(new AuthResponse("Nếu email tồn tại và đã xác nhận, hướng dẫn đặt lại mật khẩu sẽ được gửi."));
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null) return BadRequest(new { message = "Không thể đặt lại mật khẩu bằng thông tin đã cung cấp." });
        try
        {
            var decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
            var result = await userManager.ResetPasswordAsync(user, decoded, request.NewPassword);
            if (result.Succeeded) return Ok(new AuthResponse("Đặt lại mật khẩu thành công."));
            return BadRequest(new { errors = result.Errors.Select(e => new { e.Code, e.Description }) });
        }
        catch (FormatException)
        {
            return BadRequest(new { message = "Token đặt lại mật khẩu không hợp lệ." });
        }
    }

    private static bool IsAdult(DateOnly dateOfBirth)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (dateOfBirth > today) return false;
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth > today.AddYears(-age)) age--;
        return age >= 18;
    }
}
