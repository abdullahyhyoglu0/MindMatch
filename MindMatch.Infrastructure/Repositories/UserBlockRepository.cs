using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;
using MindMatch.Infrastructure.Persistence;

namespace MindMatch.Infrastructure.Repositories;

public sealed class UserBlockRepository : IUserBlockRepository
{
    private readonly MindMatchDbContext _context;

    public UserBlockRepository(MindMatchDbContext context) => _context = context;

    public Task<bool> IsBlockedEitherWayAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken = default) =>
        _context.UserBlocks.AnyAsync(x =>
            (x.BlockerId == userId && x.BlockedUserId == otherUserId) ||
            (x.BlockerId == otherUserId && x.BlockedUserId == userId), cancellationToken);

    public Task<bool> IsBlockedByAsync(Guid blockerId, Guid otherUserId, CancellationToken cancellationToken = default) =>
        _context.UserBlocks.AnyAsync(x =>
            x.BlockerId == blockerId && x.BlockedUserId == otherUserId, cancellationToken);

    public Task<List<Guid>> GetBlockedUserIdsAsync(Guid blockerId, CancellationToken cancellationToken = default) =>
        _context.UserBlocks.Where(x => x.BlockerId == blockerId)
            .Select(x => x.BlockedUserId).ToListAsync(cancellationToken);

    public Task<List<Guid>> GetRelatedUserIdsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.UserBlocks.Where(x => x.BlockerId == userId || x.BlockedUserId == userId)
            .Select(x => x.BlockerId == userId ? x.BlockedUserId : x.BlockerId)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(UserBlock block, CancellationToken cancellationToken = default)
    {
        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO user_blocks ("Id", "BlockerId", "BlockedUserId", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ({block.Id}, {block.BlockerId}, {block.BlockedUserId}, {block.CreatedAtUtc}, {block.UpdatedAtUtc})
            ON CONFLICT ("BlockerId", "BlockedUserId") DO NOTHING
            """, cancellationToken);
    }

    public async Task RemoveAsync(Guid blockerId, Guid blockedUserId, CancellationToken cancellationToken = default)
    {
        var block = await _context.UserBlocks.SingleOrDefaultAsync(x =>
            x.BlockerId == blockerId && x.BlockedUserId == blockedUserId, cancellationToken);
        if (block is not null)
            _context.UserBlocks.Remove(block);
    }
}
