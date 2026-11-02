namespace DatingApp.Api.Modules.ChatNotifications.DTOs;

public sealed record Gift(string Code, string Name, string Description);

public static class GiftCatalog
{
    public static readonly Gift[] Items = [
        new("tulip", "Một nhành hoa", "Thay cho một lời chào."),
        new("letter", "Thư tay", "Gửi chút chân thành đến người ấy."),
        new("bear", "Một cái ôm", "Dịu dàng cho một ngày thật dài.")
    ];
}
