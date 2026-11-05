using System.Security.Cryptography;
using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.Auth.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.Api.Modules.Auth.Services;

internal static class AdminProvisioning
{
    private const string Email = "admin@mylove.test";

    internal static async Task CreateLocalAdminAsync(IServiceProvider services)
    {
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var db = services.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var existing = await users.FindByEmailAsync(Email);
        if (existing is not null)
        {
            if (!await users.IsInRoleAsync(existing, "Admin"))
                throw new InvalidOperationException($"Email {Email} đã thuộc về một tài khoản khác.");
            Console.WriteLine($"Tài khoản Admin {Email} đã tồn tại.");
            return;
        }

        var user = new ApplicationUser
        {
            UserName = Email, Email = Email, EmailConfirmed = true,
            DateOfBirth = new DateOnly(2000, 1, 1)
        };
        var password = "Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
            throw new InvalidOperationException(string.Join(" ", created.Errors.Select(error => error.Description)));
        await GrantRoleAsync(users, roles, user);
        await transaction.CommitAsync();

        Console.WriteLine($"Email: {Email}");
        Console.WriteLine($"Mật khẩu: {password}");
    }

    internal static async Task GrantRoleAsync(UserManager<ApplicationUser> users,
        RoleManager<IdentityRole<Guid>> roles, ApplicationUser user)
    {
        if (!await roles.RoleExistsAsync("Admin"))
        {
            var roleResult = await roles.CreateAsync(new IdentityRole<Guid>("Admin") { Id = Guid.NewGuid() });
            if (!roleResult.Succeeded) throw new InvalidOperationException("Không tạo được vai trò Admin.");
        }
        var result = await users.AddToRoleAsync(user, "Admin");
        if (!result.Succeeded && !await users.IsInRoleAsync(user, "Admin"))
            throw new InvalidOperationException("Không cấp được vai trò Admin.");
    }
}
