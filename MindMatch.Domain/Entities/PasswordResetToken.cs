using MindMatch.Domain.Common;

namespace MindMatch.Domain.Entities;

public sealed class PasswordResetToken : BaseEntity
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? UsedAtUtc { get; private set; }
    public int Version { get; private set; }
    public bool IsUsed => UsedAtUtc.HasValue;

    private PasswordResetToken() { }

    public PasswordResetToken(Guid userId, string tokenHash, DateTime expiresAtUtc)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Parola sıfırlama token bilgileri geçersiz.");
        if (expiresAtUtc <= DateTime.UtcNow) throw new ArgumentException("Token süresi gelecekte olmalıdır.");
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAtUtc = DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc);
    }

    public bool IsActive(DateTime nowUtc) => !IsUsed && nowUtc < ExpiresAtUtc;

    public void MarkUsed(DateTime nowUtc)
    {
        if (!IsActive(nowUtc)) throw new InvalidOperationException("Token geçersiz veya süresi dolmuş.");
        UsedAtUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        Version++;
        MarkAsUpdated();
    }

    public void Invalidate(DateTime nowUtc)
    {
        if (IsUsed) return;
        UsedAtUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        Version++;
        MarkAsUpdated();
    }
}
