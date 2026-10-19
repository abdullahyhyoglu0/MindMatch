using System.ComponentModel.DataAnnotations;

namespace MindMatch.Application.Dtos.Auth;

public class RegisterRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [MaxLength(40)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Nickname { get; set; }
}
