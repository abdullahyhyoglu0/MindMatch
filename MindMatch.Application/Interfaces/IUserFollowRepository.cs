using MindMatch.Domain.Entities;

namespace MindMatch.Application.Interfaces;

public interface IUserFollowRepository
{
    Task<UserFollow?> GetAsync(Guid followerId, Guid followedId, CancellationToken cancellationToken = default);
    Task<(UserFollow? Outgoing, UserFollow? Incoming)> GetPairAsync(Guid viewerId, Guid targetUserId,
        CancellationToken cancellationToken = default);
    Task<bool> IsAcceptedAsync(Guid followerId, Guid followedId, CancellationToken cancellationToken = default);
    Task<int> CountAcceptedFollowersAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<int> CountAcceptedFollowingAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<UserFollow>> GetAcceptedFollowersAsync(Guid userId, int skip, int take,
        CancellationToken cancellationToken = default);
    Task<List<UserFollow>> GetAcceptedFollowingAsync(Guid userId, int skip, int take,
        CancellationToken cancellationToken = default);
    Task<List<Guid>> GetAcceptedFollowedIdsAsync(Guid followerId, CancellationToken cancellationToken = default);
    Task<List<UserFollow>> GetIncomingPendingAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(UserFollow follow, CancellationToken cancellationToken = default);
    Task RemoveAsync(UserFollow follow, CancellationToken cancellationToken = default);
    Task RemoveBetweenAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken = default);
}
