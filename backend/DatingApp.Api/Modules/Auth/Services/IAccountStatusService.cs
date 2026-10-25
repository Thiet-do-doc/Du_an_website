namespace DatingApp.Api.Modules.Auth.Services;

public sealed record AccountStatusChangeResult(bool Found, bool Succeeded, IReadOnlyCollection<string> Errors);

// Shared contract for moderators to change account state through the Auth module.
public interface IAccountStatusService
{
    Task<AccountStatusChangeResult> SetActiveAsync(string userId, bool isActive, CancellationToken cancellationToken = default);
}
