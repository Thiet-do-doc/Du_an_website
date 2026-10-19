using System.ComponentModel.DataAnnotations;
namespace DatingApp.Api.Modules.Auth.DTOs;
public sealed class EmailRequest
{
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
}
public sealed class ConfirmEmailRequest
{
    public Guid UserId { get; set; }
    [Required, StringLength(4096)] public string Token { get; set; } = string.Empty;
}
public sealed class ResetPasswordRequest
{
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(4096)] public string Token { get; set; } = string.Empty;
    [Required, StringLength(128, MinimumLength = 10)] public string Password { get; set; } = string.Empty;
    [Required, Compare(nameof(Password))] public string ConfirmPassword { get; set; } = string.Empty;
}
