namespace DatingApp.Api.Modules.ChatNotifications.Services;

public sealed class ChatDomainException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

/// <summary>Dependency not merged/configured yet. API returns 503 and Hub fails closed.</summary>
public sealed class ChatDependencyUnavailableException(string dependency)
    : Exception($"Chat đang chờ tích hợp dependency: {dependency}.")
{
    public string Dependency { get; } = dependency;
}
