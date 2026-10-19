using System.ComponentModel.DataAnnotations;

namespace MindMatch.Application.Dtos.Auth;

public sealed class ForgotPasswordRequest
{
    [Required, EmailAddress, MaxLength(256)] public string Email { get; set; } = string.Empty;
}

public sealed class ResetPasswordRequest
{
    [Required] public string Token { get; set; } = string.Empty;
    [Required, MinLength(8), MaxLength(100)] public string NewPassword { get; set; } = string.Empty;
}
