namespace DatingApp.Api.Modules.ChatNotifications.Services;

public static class MessageRules
{
    public static string Validate(string? content, Guid clientMessageId)
    {
        var trimmed = content?.Trim() ?? string.Empty;
        if (clientMessageId == Guid.Empty)
            throw new ChatDomainException(400, "clientMessageId phải là UUID hợp lệ.");
        if (trimmed.Length == 0)
            throw new ChatDomainException(400, "Tin nhắn không được để trống.");
        if (trimmed.Length > 2000)
            throw new ChatDomainException(400, "Tin nhắn không được vượt quá 2000 ký tự.");
        return trimmed;
    }
}
