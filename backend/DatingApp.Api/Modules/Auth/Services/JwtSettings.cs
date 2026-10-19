using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace DatingApp.Api.Modules.Auth.Services;

public sealed class JwtSettings
{
    public string Issuer { get; }
    public string Audience { get; }
    public int ExpireMinutes { get; }
    public SymmetricSecurityKey SigningKey { get; }

    public JwtSettings(IConfiguration configuration)
    {
        var key = configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
            throw new InvalidOperationException("Jwt:Key phải có ít nhất 32 byte UTF-8. Cấu hình bằng User Secrets hoặc Jwt__Key.");
        Issuer = configuration["Jwt:Issuer"] ?? string.Empty;
        Audience = configuration["Jwt:Audience"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience))
            throw new InvalidOperationException("Phải cấu hình Jwt:Issuer và Jwt:Audience.");
        if (!int.TryParse(configuration["Jwt:ExpireMinutes"], out var minutes) || minutes <= 0)
            throw new InvalidOperationException("Jwt:ExpireMinutes phải là số nguyên dương.");
        ExpireMinutes = minutes;
        SigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }
}
