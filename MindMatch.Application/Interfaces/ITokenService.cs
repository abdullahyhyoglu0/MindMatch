
namespace MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;

public interface ITokenService
{
    string GenerateAccessToken(Guid userId, string email, UserRole role = UserRole.User, int sessionVersion = 0);

    string GenerateRefreshToken();

    DateTime GetAccessTokenExpiryUtc();

    DateTime GetRefreshTokenExpiryUtc();
}
