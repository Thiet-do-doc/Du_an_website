namespace DatingApp.Api.Modules.ChatNotifications.Services;

public static class MessageRules
{
    public static string Validate(string? content, Guid clientMessageId)
    {
        if (clientMessageId == Guid.Empty)
            throw new ChatDomainException(400, "clientMessageId không hợp lệ.");
        var normalized = content?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 2000)
            throw new ChatDomainException(400, "Tin nhắn phải có 1–2000 ký tự.");
        return normalized;
    }
}
