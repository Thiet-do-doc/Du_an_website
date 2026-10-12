namespace DatingApp.Api.Modules.ChatNotifications.Services;

public sealed class ChatDomainException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
