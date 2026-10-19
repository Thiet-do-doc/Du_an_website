using System.Net;
using System.Net.Mail;
namespace DatingApp.Api.Modules.Auth.Services;
public interface IAuthEmailSender
{
    Task SendAsync(string email, string subject, string link, CancellationToken cancellationToken = default);
}
public sealed class AuthEmailSender(IConfiguration configuration, IWebHostEnvironment environment) : IAuthEmailSender
{
    public async Task SendAsync(string email, string subject, string link, CancellationToken cancellationToken = default)
    {
        using var message = new MailMessage(configuration["Email:From"] ?? "noreply@datingapp.local", email, subject,
            "Mở liên kết sau để tiếp tục. Nếu không yêu cầu, hãy bỏ qua email này.\n\n" + link);
        using var client = new SmtpClient();
        if (environment.IsDevelopment() && configuration["Email:Delivery"] == "Pickup")
        {
            var directory = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "../../.artifacts/mail"));
            Directory.CreateDirectory(directory);
            client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            client.PickupDirectoryLocation = directory;
        }
        else
        {
            client.Host = configuration["Email:SmtpHost"] ?? throw new InvalidOperationException("Chưa cấu hình Email:SmtpHost.");
            client.Port = configuration.GetValue("Email:SmtpPort", 587);
            client.EnableSsl = true;
            client.Credentials = new NetworkCredential(configuration["Email:Username"], configuration["Email:Password"]);
        }
        try { await client.SendMailAsync(message, cancellationToken); }
        catch (SmtpException exception) { throw new AuthEmailDeliveryException(exception); }
    }
}
