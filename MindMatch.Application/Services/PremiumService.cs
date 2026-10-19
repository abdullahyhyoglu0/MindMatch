using MindMatch.Application.Dtos.Premium;
using MindMatch.Application.Interfaces;

namespace MindMatch.Application.Services;

public sealed class PremiumService : IPremiumService
{
    private readonly IPremiumEntitlementRepository _entitlements;
    public PremiumService(IPremiumEntitlementRepository entitlements) => _entitlements = entitlements;

    public Task<bool> IsPremiumAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _entitlements.HasActiveAsync(userId, DateTime.UtcNow, cancellationToken);

    public async Task<int> GetConversationRetentionDaysAsync(Guid user1Id, Guid user2Id, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var active1 = await _entitlements.HasActiveAsync(user1Id, now, cancellationToken);
        var active2 = await _entitlements.HasActiveAsync(user2Id, now, cancellationToken);
        // Conversation retention is fixed when the conversation starts; either participant's active entitlement enables the shared 7-day policy.
        return active1 || active2 ? 7 : 1;
    }

    public async Task<PremiumStatusResponse> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entitlement = await _entitlements.GetActiveAsync(userId, DateTime.UtcNow, cancellationToken);
        return new PremiumStatusResponse { IsPremium = entitlement is not null, ExpiresAtUtc = entitlement?.ExpiresAtUtc };
    }
}
