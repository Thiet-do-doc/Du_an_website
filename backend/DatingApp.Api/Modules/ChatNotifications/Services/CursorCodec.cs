using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace DatingApp.Api.Modules.ChatNotifications.Services;

// The cursor encodes a (UTC tick, message UUID) keyset, NOT an offset.
public static class CursorCodec
{
    public static string Encode(DateTimeOffset sentAt, Guid id) =>
        WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes($"{sentAt.UtcTicks}:{id:N}"));

    public static bool TryDecode(string input, out DateTimeOffset sentAt, out Guid id)
    {
        sentAt = default;
        id = default;
        try
        {
            var value = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(input));
            var parts = value.Split(':');
            if (parts.Length != 2 || !long.TryParse(parts[0], NumberStyles.None,
                    CultureInfo.InvariantCulture, out var ticks)
                || !Guid.TryParseExact(parts[1], "N", out id)) return false;
            sentAt = new DateTimeOffset(new DateTime(ticks, DateTimeKind.Utc));
            return true;
        }
        catch (Exception e) when (e is FormatException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
