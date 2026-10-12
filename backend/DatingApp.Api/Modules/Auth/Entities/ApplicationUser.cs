using Microsoft.AspNetCore.Identity;

namespace DatingApp.Api.Modules.Auth.Entities;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.NewGuid();
    }

    // Ngày sinh do người dùng khai báo khi đăng ký.
    public DateOnly DateOfBirth { get; set; }

    // Thời điểm tạo tài khoản, thống nhất lưu theo UTC.
    public DateTimeOffset CreatedAtUtc { get; set; }
        = DateTimeOffset.UtcNow;
}