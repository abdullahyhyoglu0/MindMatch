using MindMatch.Application.Dtos.Premium;

namespace MindMatch.Application.Interfaces;

public interface IPremiumService
{
    Task<bool> IsPremiumAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<int> GetConversationRetentionDaysAsync(Guid user1Id, Guid user2Id, CancellationToken cancellationToken = default);
    Task<PremiumStatusResponse> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default);
}
