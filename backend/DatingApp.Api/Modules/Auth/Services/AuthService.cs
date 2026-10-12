using DatingApp.Api.Modules.Auth.DTOs;
using DatingApp.Api.Modules.Auth.Entities;
using Microsoft.AspNetCore.Identity;

namespace DatingApp.Api.Modules.Auth.Services;

public sealed class AuthService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public AuthService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<(IdentityResult Result, Guid? UserId)> RegisterAsync(
        RegisterRequest request)
    {
        // Thống nhất kiểm tra ngày theo UTC.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (request.DateOfBirth is not DateOnly dateOfBirth
            || dateOfBirth == DateOnly.MinValue
            || dateOfBirth > today)
        {
            return (IdentityResult.Failed(new IdentityError
            {
                Code = "DateOfBirth",
                Description = "Ngày sinh không hợp lệ."
            }), null);
        }

        if (dateOfBirth > today.AddYears(-18))
        {
            return (IdentityResult.Failed(new IdentityError
            {
                Code = "DateOfBirth",
                Description = "Bạn phải đủ 18 tuổi để đăng ký."
            }), null);
        }

        var email = request.Email.Trim();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DateOfBirth = dateOfBirth,
            EmailConfirmed = false
        };

        // Identity kiểm tra tài khoản, mật khẩu,
        // băm mật khẩu và lưu vào database.
        var result = await _userManager.CreateAsync(
            user,
            request.Password);

        if (!result.Succeeded)
        {
            return (result, null);
        }

        return (result, user.Id);
    }
}