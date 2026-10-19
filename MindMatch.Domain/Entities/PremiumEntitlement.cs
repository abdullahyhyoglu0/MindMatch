using MindMatch.Domain.Common;

namespace MindMatch.Domain.Entities;

public enum PremiumEntitlementSource { AppStore = 1, PlayStore = 2, PaymentProvider = 3, Promotion = 4 }

public sealed class PremiumEntitlement : BaseEntity
{
    public Guid UserId { get; private set; }
    public PremiumEntitlementSource Source { get; private set; }
    public string ExternalReference { get; private set; } = string.Empty;
    public DateTime StartsAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public bool IsRevoked { get; private set; }

    private PremiumEntitlement() { }

    public PremiumEntitlement(Guid userId, PremiumEntitlementSource source, string externalReference,
        DateTime startsAtUtc, DateTime expiresAtUtc)
    {
        if (userId == Guid.Empty || !Enum.IsDefined(source) || string.IsNullOrWhiteSpace(externalReference) || externalReference.Length > 200)
            throw new ArgumentException("Premium entitlement bilgileri geçersiz.");
        if (expiresAtUtc <= startsAtUtc) throw new ArgumentException("Premium bitiş zamanı başlangıçtan sonra olmalıdır.");
        UserId = userId;
        Source = source;
        ExternalReference = externalReference.Trim();
        StartsAtUtc = DateTime.SpecifyKind(startsAtUtc, DateTimeKind.Utc);
        ExpiresAtUtc = DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc);
    }

    public bool IsActiveAt(DateTime nowUtc) => !IsRevoked && StartsAtUtc <= nowUtc && ExpiresAtUtc > nowUtc;
    public void Revoke() { IsRevoked = true; MarkAsUpdated(); }
}
