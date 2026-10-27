using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;
using MindMatch.Infrastructure.Persistence;

namespace MindMatch.Infrastructure.Repositories;

public sealed class UserFollowRepository : IUserFollowRepository
{
    private readonly MindMatchDbContext _context;
    public UserFollowRepository(MindMatchDbContext context) => _context = context;

    public Task<UserFollow?> GetAsync(Guid followerId, Guid followedId, CancellationToken cancellationToken = default) =>
        _context.UserFollows.SingleOrDefaultAsync(x => x.FollowerId == followerId && x.FollowedId == followedId, cancellationToken);

    public async Task<(UserFollow? Outgoing, UserFollow? Incoming)> GetPairAsync(Guid viewerId, Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _context.UserFollows.Where(x =>
            (x.FollowerId == viewerId && x.FollowedId == targetUserId) ||
            (x.FollowerId == targetUserId && x.FollowedId == viewerId)).ToListAsync(cancellationToken);
        return (rows.SingleOrDefault(x => x.FollowerId == viewerId), rows.SingleOrDefault(x => x.FollowerId == targetUserId));
    }

    public Task<bool> IsAcceptedAsync(Guid followerId, Guid followedId, CancellationToken cancellationToken = default) =>
        _context.UserFollows.AnyAsync(x => x.FollowerId == followerId && x.FollowedId == followedId && x.Status == FollowStatus.Accepted, cancellationToken);
    public Task<int> CountAcceptedFollowersAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.UserFollows.CountAsync(x => x.FollowedId == userId && x.Status == FollowStatus.Accepted, cancellationToken);
    public Task<int> CountAcceptedFollowingAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.UserFollows.CountAsync(x => x.FollowerId == userId && x.Status == FollowStatus.Accepted, cancellationToken);
    public Task<List<UserFollow>> GetAcceptedFollowersAsync(Guid userId, int skip, int take, CancellationToken cancellationToken = default) =>
        _context.UserFollows.Where(x => x.FollowedId == userId && x.Status == FollowStatus.Accepted)
            .OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
    public Task<List<UserFollow>> GetAcceptedFollowingAsync(Guid userId, int skip, int take, CancellationToken cancellationToken = default) =>
        _context.UserFollows.Where(x => x.FollowerId == userId && x.Status == FollowStatus.Accepted)
            .OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
    public Task<List<Guid>> GetAcceptedFollowedIdsAsync(Guid followerId, CancellationToken cancellationToken = default) =>
        _context.UserFollows.Where(x => x.FollowerId == followerId && x.Status == FollowStatus.Accepted)
            .Select(x => x.FollowedId).ToListAsync(cancellationToken);
    public Task<List<UserFollow>> GetIncomingPendingAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.UserFollows.Where(x => x.FollowedId == userId && x.Status == FollowStatus.Pending)
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
    public Task AddAsync(UserFollow follow, CancellationToken cancellationToken = default) =>
        _context.UserFollows.AddAsync(follow, cancellationToken).AsTask();
    public Task RemoveAsync(UserFollow follow, CancellationToken cancellationToken = default)
    { _context.UserFollows.Remove(follow); return Task.CompletedTask; }
    public async Task RemoveBetweenAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken = default) =>
        _ = await _context.UserFollows.Where(x => (x.FollowerId == userId && x.FollowedId == otherUserId) ||
            (x.FollowerId == otherUserId && x.FollowedId == userId)).ExecuteDeleteAsync(cancellationToken);
}
