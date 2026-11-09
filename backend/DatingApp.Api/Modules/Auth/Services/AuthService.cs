using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.Auth.DTOs;
using DatingApp.Api.Modules.Auth.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
namespace DatingApp.Api.Modules.Auth.Services;
public sealed record AuthSession(LoginResponse Response, string RefreshToken, DateTimeOffset RefreshExpiresAt);
public sealed class AuthService(UserManager<ApplicationUser> users, AppDbContext db, JwtSettings jwt,
    IAuthEmailSender emailSender, IConfiguration configuration, AuthConnectionRegistry connections, TimeProvider clock)
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ResendDelay = TimeSpan.FromMinutes(1);
    private const int MaxCodeFailures = 5;
    public async Task<(IdentityResult Result, Guid? UserId)> RegisterAsync(RegisterRequest request)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.DateOfBirth is not DateOnly birthday || birthday == DateOnly.MinValue || birthday > today)
            return (Error("DateOfBirth", "Ngày sinh không hợp lệ."), null);
        if (birthday > today.AddYears(-18)) return (Error("UnderAge", "Bạn phải đủ 18 tuổi."), null);
        var email = request.Email.Trim();
        if (await users.FindByEmailAsync(email) is not null) return (Error("DuplicateEmail", "Email này đã được sử dụng."), null);
        var user = new ApplicationUser { UserName = email, Email = email, DateOfBirth = birthday, LockoutEnabled = true };
        IdentityResult result;
        try { result = await users.CreateAsync(user, request.Password); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "UserNameIndex" })
        { db.ChangeTracker.Clear(); return (Error("DuplicateEmail", "Email này đã được sử dụng."), null); }
        if (!result.Succeeded) return (result, null);
        await SendConfirmationCodeAsync(user.Id);
        return (result, user.Id);
    }
    public async Task SendConfirmationAsync(string email)
    {
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is not null) await SendConfirmationCodeAsync(user.Id);
    }
    private byte[] CodeHash(ApplicationUser user, string code) =>
        HMACSHA256.HashData(jwt.SigningKey.Key, Encoding.UTF8.GetBytes($"email-confirm-v1:{user.Id:N}:{user.NormalizedEmail}:{code}"));

    private async Task SendConfirmationCodeAsync(Guid id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = await LockUserAsync(id);
        if (user is null || !user.IsActive || user.EmailConfirmed || await users.IsLockedOutAsync(user)) return;
        var now = clock.GetUtcNow();
        if (user.ConfirmationCodeExpiresAt > now + CodeLifetime - ResendDelay) return;
        string code;
        byte[] hash;
        do
        {
            code = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);
            hash = CodeHash(user, code);
        } while (user.ConfirmationCodeHash is { } previous && CryptographicOperations.FixedTimeEquals(hash, previous));
        user.ConfirmationCodeHash = hash;
        user.ConfirmationCodeExpiresAt = now + CodeLifetime;
        user.ConfirmationCodeFailures = 0;
        await db.SaveChangesAsync();
        await emailSender.SendAsync(user.Email!, "Mã xác nhận email mylove",
            $"Mã xác nhận của bạn: {code}\n\nMã có hiệu lực trong 5 phút. Nếu bạn không đăng ký mylove, hãy bỏ qua email này.");
        await transaction.CommitAsync();
    }

    public async Task<AuthSession?> VerifyEmailCodeAsync(VerifyEmailCodeRequest request)
    {
        var found = await users.FindByEmailAsync(request.Email.Trim());
        if (found is null) return null;
        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = await LockUserAsync(found.Id);
        if (user is null || !user.IsActive || user.EmailConfirmed || await users.IsLockedOutAsync(user)
            || user.ConfirmationCodeHash is not { } hash || user.ConfirmationCodeExpiresAt is not { } expiry
            || expiry <= clock.GetUtcNow() || user.ConfirmationCodeFailures >= MaxCodeFailures) return null;
        if (!CryptographicOperations.FixedTimeEquals(hash, CodeHash(user, request.Code)))
        {
            user.ConfirmationCodeFailures++;
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return null;
        }
        user.EmailConfirmed = true;
        user.ConfirmationCodeHash = null;
        user.ConfirmationCodeExpiresAt = null;
        user.ConfirmationCodeFailures = 0;
        var session = await IssueAsync(user, Guid.NewGuid(), clock.GetUtcNow().AddDays(7));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return session;
    }
    public async Task<IdentityResult> ConfirmEmailAsync(ConfirmEmailRequest request)
    {
        var decoded = Decode(request.Token);
        if (decoded is null) return Error("InvalidToken", "Liên kết không hợp lệ hoặc đã hết hạn.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = await LockUserAsync(request.UserId);
        if (user is null || !user.IsActive || user.EmailConfirmed || user.ConfirmationCodeHash is not null) return Error("InvalidToken", "Liên kết không hợp lệ hoặc đã sử dụng.");
        var result = await users.ConfirmEmailAsync(user, decoded);
        if (result.Succeeded) await transaction.CommitAsync();
        return result;
    }
    public async Task ForgotPasswordAsync(string email)
    {
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null || !user.EmailConfirmed || !user.IsActive) return;
        var token = Encode(await users.GeneratePasswordResetTokenAsync(user));
        await emailSender.SendAsync(user.Email!, "Đặt lại mật khẩu DatingApp",
            "Mở liên kết sau để đặt lại mật khẩu. Nếu không yêu cầu, hãy bỏ qua email này.\n\n" +
            Link("reset-password", new Dictionary<string, string?> { ["email"] = user.Email, ["token"] = token }));
    }
    public async Task<IdentityResult> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var decoded = Decode(request.Token);
        var found = await users.FindByEmailAsync(request.Email.Trim());
        if (decoded is null || found is null) return Error("InvalidToken", "Liên kết không hợp lệ hoặc đã hết hạn.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = await LockUserAsync(found.Id);
        if (user is null || !user.IsActive || !user.EmailConfirmed) return Error("InvalidToken", "Liên kết không hợp lệ.");
        var result = await users.ResetPasswordAsync(user, decoded, request.Password);
        if (!result.Succeeded) return result;
        await users.ResetAccessFailedCountAsync(user);
        await users.SetLockoutEndDateAsync(user, null);
        await RevokeUserAsync(user.Id);
        await transaction.CommitAsync();
        connections.Disconnect(user.Id);
        return result;
    }
    public async Task<AuthSession?> LoginAsync(LoginRequest request)
    {
        var found = await users.FindByEmailAsync(request.Email.Trim());
        if (found is null) return null;
        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = await LockUserAsync(found.Id);
        if (user is null || !user.IsActive || await users.IsLockedOutAsync(user)) return null;
        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            await transaction.CommitAsync();
            return null;
        }
        if (!user.EmailConfirmed) return null;
        await users.ResetAccessFailedCountAsync(user);
        var session = await IssueAsync(user, Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(7));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return session;
    }
    public async Task<AuthSession?> RefreshAsync(string rawToken)
    {
        var hash = Hash(rawToken);
        var found = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash);
        if (found is null) return null;
        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = await LockUserAsync(found.UserId);
        var rows = await db.RefreshTokens.FromSqlInterpolated($"SELECT * FROM \"RefreshTokens\" WHERE \"Id\" = {found.Id} FOR UPDATE").ToListAsync();
        var stored = rows.SingleOrDefault();
        if (stored is null || user is null) return null;
        if (stored.RevokedAtUtc is not null)
        {
            // Tái sử dụng refresh token: thu hồi cả phiên, kể cả token mới đã phát.
            await RevokeSessionAsync(stored.SessionId);
            await transaction.CommitAsync();
            connections.DisconnectSession(stored.SessionId);
            return null;
        }
        if (stored.ExpiresAtUtc <= DateTimeOffset.UtcNow || !user.IsActive || !user.EmailConfirmed || await users.IsLockedOutAsync(user))
        {
            await RevokeSessionAsync(stored.SessionId);
            await transaction.CommitAsync();
            return null;
        }
        stored.RevokedAtUtc = DateTimeOffset.UtcNow;
        var session = await IssueAsync(user, stored.SessionId, stored.ExpiresAtUtc);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return session;
    }
    public async Task LogoutAsync(string? rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return;
        var hash = Hash(rawToken);
        var token = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash);
        if (token is null) return;
        await using var transaction = await db.Database.BeginTransactionAsync();
        await LockUserAsync(token.UserId);
        await RevokeSessionAsync(token.SessionId);
        await transaction.CommitAsync();
        connections.DisconnectSession(token.SessionId);
    }
    private async Task<AuthSession> IssueAsync(ApplicationUser user, Guid sessionId, DateTimeOffset refreshExpiry)
    {
        var raw = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(48));
        db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, SessionId = sessionId, TokenHash = Hash(raw),
            CreatedAtUtc = DateTimeOffset.UtcNow, ExpiresAtUtc = refreshExpiry });
        var expiresAt = DateTime.UtcNow.AddMinutes(jwt.ExpireMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!), new(ClaimTypes.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), new("sid", sessionId.ToString()),
            new("security_stamp", user.SecurityStamp!)
        };
        claims.Add(new Claim(ClaimTypes.Role, await users.IsInRoleAsync(user, "Admin") ? "Admin" : "User"));
        var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience, claims, expires: expiresAt,
            signingCredentials: new SigningCredentials(jwt.SigningKey, SecurityAlgorithms.HmacSha256));
        return new AuthSession(new LoginResponse { Token = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt = expiresAt, UserId = user.Id, Email = user.Email! }, raw, refreshExpiry);
    }
    private async Task<ApplicationUser?> LockUserAsync(Guid userId)
    {
        // Tránh dùng bản tracked đã đọc trước khi một transaction khác đổi trạng thái/stamp.
        db.ChangeTracker.Clear();
        return (await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {userId} FOR UPDATE").ToListAsync()).SingleOrDefault();
    }
    private Task<int> RevokeSessionAsync(Guid id) => db.RefreshTokens.Where(x => x.SessionId == id && x.RevokedAtUtc == null)
        .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, DateTimeOffset.UtcNow));
    private Task<int> RevokeUserAsync(Guid id) => db.RefreshTokens.Where(x => x.UserId == id && x.RevokedAtUtc == null)
        .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, DateTimeOffset.UtcNow));
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Encode(string value) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(value));
    private static string? Decode(string value)
    { try { return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(value)); } catch (FormatException) { return null; } }
    private string Link(string path, Dictionary<string, string?> query) =>
        QueryHelpers.AddQueryString(configuration["Auth:FrontendUrl"]!.TrimEnd('/') + "/" + path, query);
    private static IdentityResult Error(string code, string message) => IdentityResult.Failed(new IdentityError { Code = code, Description = message });
}
