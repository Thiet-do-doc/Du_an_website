using System.Globalization;
using System.Text;

namespace DatingApp.Api.Modules.ChatNotifications.Services;

/// <summary>Opaque keyset cursor: UTC ticks and UUID, base64url encoded.</summary>
public static class CursorCodec
{
    public static string Encode(DateTimeOffset sentAt, Guid id)
    {
        var plain = string.Create(CultureInfo.InvariantCulture, $"{sentAt.UtcTicks}:{id:N}");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(plain))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static bool TryDecode(string? cursor, out DateTimeOffset sentAt, out Guid id)
    {
        sentAt = default;
        id = Guid.Empty;
        if (string.IsNullOrWhiteSpace(cursor)) return false;
        try
        {
            var value = cursor.Replace('-', '+').Replace('_', '/');
            value = value.PadRight(value.Length + ((4 - value.Length % 4) % 4), '=');
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(value));
            var parts = decoded.Split(':', 2);
            if (parts.Length != 2 || !long.TryParse(parts[0], NumberStyles.None,
                    CultureInfo.InvariantCulture, out var ticks) || ticks < DateTimeOffset.MinValue.UtcTicks ||
                ticks > DateTimeOffset.MaxValue.UtcTicks || !Guid.TryParseExact(parts[1], "N", out id))
                return false;
            sentAt = new DateTimeOffset(ticks, TimeSpan.Zero);
            return id != Guid.Empty;
        }
        catch (FormatException) { return false; }
        catch (ArgumentOutOfRangeException) { return false; }
    }
}
