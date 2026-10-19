
using Microsoft.AspNetCore.Identity;
using MindMatch.Application.Interfaces;

namespace MindMatch.Infrastructure.Security;

public class PasswordHasherService : IPasswordHasher
{
    private readonly PasswordHasher<object> _hasher = new();

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        return _hasher.HashPassword(new object(), password);
    }

    public bool VerifyPassword(
        string password,
        string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        var result = _hasher.VerifyHashedPassword(
            new object(),
            passwordHash,
            password);

        return result != PasswordVerificationResult.Failed;
    }
}
