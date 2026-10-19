using System.ComponentModel.DataAnnotations;

namespace MindMatch.Application.Dtos.Auth;

public sealed class ChangePasswordRequest
{
    [Required] public string CurrentPassword { get; set; } = string.Empty;
    [Required, MinLength(8), MaxLength(100)] public string NewPassword { get; set; } = string.Empty;
}

public sealed class UserMeResponse
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public string Role { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
}
