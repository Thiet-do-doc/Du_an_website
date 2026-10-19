using System.ComponentModel.DataAnnotations;

namespace DatingApp.Api.Modules.Auth.DTOs;

public class LoginRequest
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;
    [Required]
    [StringLength(128)]
    public string Password { get; set; } = string.Empty;
}
