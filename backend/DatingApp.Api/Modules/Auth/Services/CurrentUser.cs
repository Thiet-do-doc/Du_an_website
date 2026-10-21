using System.Security.Claims;
namespace DatingApp.Api.Modules.Auth.Services;
public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Email { get; }
    bool IsAdmin { get; }
}
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public string? Email => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email);
    public bool IsAdmin => accessor.HttpContext?.User.IsInRole("Admin") == true;
}
