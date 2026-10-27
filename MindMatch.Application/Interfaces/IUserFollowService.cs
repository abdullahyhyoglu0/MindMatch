using MindMatch.Application.Dtos.Follow;
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Interfaces;

public interface IUserFollowService
{
    Task<FollowResponse> FollowAsync(Guid followerId, Guid targetUserId, CancellationToken cancellationToken = default);
    Task UnfollowAsync(Guid followerId, Guid targetUserId, CancellationToken cancellationToken = default);
    Task RemoveFollowerAsync(Guid userId, Guid followerId, CancellationToken cancellationToken = default);
    Task AcceptAsync(Guid userId, Guid followerId, CancellationToken cancellationToken = default);
    Task RejectAsync(Guid userId, Guid followerId, CancellationToken cancellationToken = default);
    Task<List<FollowResponse>> GetIncomingAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<FollowListResponse> GetFollowersAsync(Guid viewerId, Guid targetUserId, int page, int pageSize,
        CancellationToken cancellationToken = default);
    Task<FollowListResponse> GetFollowingAsync(Guid viewerId, Guid targetUserId, int page, int pageSize,
        CancellationToken cancellationToken = default);
    Task<FollowRelation> GetRelationAsync(Guid viewerId, Guid targetUserId, CancellationToken cancellationToken = default);
}
