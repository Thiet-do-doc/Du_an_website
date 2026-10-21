using DatingApp.Api.Modules.Auth.Entities;
using Microsoft.AspNetCore.Identity;

namespace DatingApp.Api.Modules.Auth.Services;

public sealed class AccountStatusService(UserManager<ApplicationUser> users) : IAccountStatusService
{
    public async Task<AccountStatusChangeResult> SetActiveAsync(string userId, bool isActive, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null) return new(false, false, Array.Empty<string>());
        user.IsActive = isActive;
        var lockout = await users.SetLockoutEndDateAsync(user,
            isActive ? DateTimeOffset.UtcNow.AddDays(-1) : DateTimeOffset.MaxValue);
        if (!lockout.Succeeded) return new(true, false, lockout.Errors.Select(x => x.Code).ToArray());
        var stamp = await users.UpdateSecurityStampAsync(user);
        return new(true, stamp.Succeeded, stamp.Errors.Select(x => x.Code).ToArray());
    }
}
