
using System.ComponentModel.DataAnnotations;

namespace MindMatch.Application.Dtos.Auth;

public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
