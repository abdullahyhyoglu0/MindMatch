using MindMatch.Domain.Entities;

namespace MindMatch.Application.Interfaces;

public interface IUserBlockRepository
{
    Task<bool> IsBlockedEitherWayAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken = default);
    Task<bool> IsBlockedByAsync(Guid blockerId, Guid otherUserId, CancellationToken cancellationToken = default);
    Task<List<Guid>> GetBlockedUserIdsAsync(Guid blockerId, CancellationToken cancellationToken = default);
    Task<List<Guid>> GetRelatedUserIdsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(UserBlock block, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid blockerId, Guid blockedUserId, CancellationToken cancellationToken = default);
}
