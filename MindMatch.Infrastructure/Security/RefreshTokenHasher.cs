
using System.Security.Cryptography;
using System.Text;

namespace MindMatch.Infrastructure.Security;

public static class RefreshTokenHasher
{
    public static string Hash(string refreshToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

        var bytes = Encoding.UTF8.GetBytes(refreshToken);
        var hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash);
    }
}
