namespace DatingApp.Api.Modules.Auth.DTOs;

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }

    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
}