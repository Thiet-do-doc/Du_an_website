using System.ComponentModel.DataAnnotations;

namespace Vo_T1_2_AuthStarter.Contracts;

public sealed record RegisterRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(10), MaxLength(100)] string Password,
    [Required] DateOnly DateOfBirth);

public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public sealed record EmailRequest([Required, EmailAddress] string Email);

public sealed record ResetPasswordRequest(
    [Required, EmailAddress] string Email,
    [Required] string Token,
    [Required, MinLength(10), MaxLength(100)] string NewPassword);

public sealed record AuthResponse(string Message, bool EmailConfirmationRequired = false);
