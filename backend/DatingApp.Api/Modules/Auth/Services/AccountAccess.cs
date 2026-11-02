using System.Collections.Concurrent;
using System.Security.Claims;
using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.Auth.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace DatingApp.Api.Modules.Auth.Services;
public sealed class AuthConnectionRegistry
{
    private readonly ConcurrentDictionary<string, (Guid UserId, Guid? SessionId, Action Abort)> _connections = new();
    public void Add(string connectionId, Guid userId, Action abort, Guid? sessionId = null) => _connections[connectionId] = (userId, sessionId, abort);
    public void Remove(string connectionId) => _connections.TryRemove(connectionId, out _);
    public void Disconnect(Guid userId)
    {
        foreach (var entry in _connections.Where(x => x.Value.UserId == userId))
            if (_connections.TryRemove(entry.Key, out var connection)) connection.Abort();
    }
    public void DisconnectSession(Guid sessionId)
    {
        foreach (var entry in _connections.Where(x => x.Value.SessionId == sessionId))
            if (_connections.TryRemove(entry.Key, out var connection)) connection.Abort();
    }
}
public interface IAccountAccess
{
    Task<bool> ValidateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task<bool> SetActiveAsync(Guid userId, bool active, CancellationToken cancellationToken = default);
}
public sealed class AccountAccess(AppDbContext db, UserManager<ApplicationUser> users, AuthConnectionRegistry connections) : IAccountAccess
{
    public async Task<bool> ValidateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(principal.FindFirstValue("exp"), out var expires)
            || expires <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return false;
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(principal.FindFirstValue("sid"), out var sessionId)) return false;
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null || !user.IsActive || !user.EmailConfirmed
            || user.SecurityStamp != principal.FindFirstValue("security_stamp")) return false;
        if (!await db.RefreshTokens.AnyAsync(x => x.UserId == userId && x.SessionId == sessionId
                && x.RevokedAtUtc == null && x.ExpiresAtUtc > DateTimeOffset.UtcNow, cancellationToken)) return false;
        // Quyền luôn lấy lại từ DB: hạ quyền có hiệu lực ngay với JWT cũ.
        var identity = (ClaimsIdentity)principal.Identity!;
        foreach (var claim in identity.FindAll(ClaimTypes.Role).ToArray()) identity.RemoveClaim(claim);
        identity.AddClaim(new Claim(ClaimTypes.Role, "User"));
        foreach (var role in await users.GetRolesAsync(user)) identity.AddClaim(new Claim(ClaimTypes.Role, role));
        return true;
    }
    public async Task<bool> SetActiveAsync(Guid userId, bool active, CancellationToken cancellationToken = default)
    {
        // Join moderation's transaction so the status, revocation and audit commit together.
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        var user = (await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {userId} FOR UPDATE")
            .ToListAsync(cancellationToken)).SingleOrDefault();
        if (user is null) return false;
        user.IsActive = active;
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        await db.RefreshTokens.Where(x => x.UserId == userId && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, DateTimeOffset.UtcNow), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            connections.Disconnect(userId);
        }
        return true;
    }
}
