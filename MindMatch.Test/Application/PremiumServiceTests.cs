using MindMatch.Application.Interfaces;
using MindMatch.Application.Services;
using MindMatch.Domain.Entities;

namespace MindMatch.Test.Application;

public sealed class PremiumServiceTests
{
    [Fact]
    public async Task ConversationRetention_IsSevenDaysWhenEitherParticipantIsPremium()
    {
        var premiumUser = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        var service = new PremiumService(new FakeEntitlementRepository(premiumUser));

        Assert.Equal(7, await service.GetConversationRetentionDaysAsync(otherUser, premiumUser));
        Assert.Equal(1, await service.GetConversationRetentionDaysAsync(otherUser, Guid.NewGuid()));
    }

    private sealed class FakeEntitlementRepository(Guid premiumUser) : IPremiumEntitlementRepository
    {
        public Task<PremiumEntitlement?> GetActiveAsync(Guid userId, DateTime nowUtc,
            CancellationToken cancellationToken = default) => Task.FromResult<PremiumEntitlement?>(null);
        public Task<bool> HasActiveAsync(Guid userId, DateTime nowUtc,
            CancellationToken cancellationToken = default) => Task.FromResult(userId == premiumUser);
        public Task AddAsync(PremiumEntitlement entitlement, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
