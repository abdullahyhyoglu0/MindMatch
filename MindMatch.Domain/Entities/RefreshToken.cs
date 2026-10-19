
using MindMatch.Domain.Common;

namespace MindMatch.Domain.Entities;

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; private set; }

    // Token'ın kendisi değil, hash'i saklanacak.
    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }
    public int Version { get; private set; } = 1;

    public bool IsRevoked => RevokedAtUtc.HasValue;

    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;

    public bool IsActive => !IsRevoked && !IsExpired;

    private RefreshToken() { }

    public RefreshToken(
        Guid userId,
        string tokenHash,
        DateTime expiresAtUtc)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("Kullanıcı kimliği geçersiz.");

        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Token hash boş olamaz.");

        if (expiresAtUtc <= DateTime.UtcNow)
            throw new ArgumentException("Token süresi gelecekte olmalıdır.");

        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
    }

    public void Revoke()
    {
        if (IsRevoked)
            return;

        RevokedAtUtc = DateTime.UtcNow;
        Version++;
        MarkAsUpdated();
    }
}
