namespace DatingApp.Api.Modules.Auth.Services;

/// <summary>Local demo adapter only. Replace with SMTP/provider implementation before deployment.</summary>
public sealed class DevelopmentEmailSender(ILogger<DevelopmentEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        // Development-only: logs email content so the team can test without a mail provider.
        logger.LogInformation("[DEV EMAIL] To={To}; Subject={Subject}; Body={Body}", to, subject, htmlBody);
        return Task.CompletedTask;
    }
}
