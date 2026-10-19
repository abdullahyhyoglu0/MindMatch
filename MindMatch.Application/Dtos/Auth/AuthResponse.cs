
namespace MindMatch.Application.Dtos.Auth;

public class AuthResponse
{
    public Guid UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string AccessToken { get; set; } = string.Empty;

    public DateTime AccessTokenExpiresAtUtc { get; set; }

    public string RefreshToken { get; set; } = string.Empty;

    public DateTime RefreshTokenExpiresAtUtc { get; set; }

    public bool RequiresEmailVerification { get; set; }
}
