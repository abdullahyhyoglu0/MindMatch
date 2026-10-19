
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;

namespace MindMatch.Infrastructure.Security;

public class TokenService : ITokenService
{
    private readonly TokenSettings _settings;

    public TokenService(IOptions<TokenSettings> options)
    {
        _settings = options.Value;

        if (string.IsNullOrWhiteSpace(_settings.Issuer))
            throw new InvalidOperationException("Token Issuer ayarlanmamış.");

        if (string.IsNullOrWhiteSpace(_settings.Audience))
            throw new InvalidOperationException("Token Audience ayarlanmamış.");

        if (Encoding.UTF8.GetByteCount(_settings.SecretKey) < 32)
            throw new InvalidOperationException(
                "Token SecretKey en az 32 byte olmalıdır.");
    }

    public string GenerateAccessToken(Guid userId, string email, UserRole role = UserRole.User, int sessionVersion = 0)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("Kullanıcı kimliği geçersiz.");

        var now = DateTime.UtcNow;

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role.ToString()),
            new Claim("tv", sessionVersion.ToString(System.Globalization.CultureInfo.InvariantCulture))
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_settings.SecretKey));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now,
            expires: GetAccessTokenExpiryUtc(),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        // Tahmin edilmesi zor, kriptografik olarak güvenli rastgele token.
        return Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(64));
    }

    public DateTime GetAccessTokenExpiryUtc()
    {
        return DateTime.UtcNow.AddMinutes(
            _settings.AccessTokenExpirationMinutes);
    }

    public DateTime GetRefreshTokenExpiryUtc()
    {
        return DateTime.UtcNow.AddDays(
            _settings.RefreshTokenExpirationDays);
    }
}
