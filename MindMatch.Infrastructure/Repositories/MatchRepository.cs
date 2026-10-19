
using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;
using MindMatch.Infrastructure.Persistence;

namespace MindMatch.Infrastructure.Repositories;

public class MatchRepository : IMatchRepository
{
    private readonly MindMatchDbContext _context;

    public MatchRepository(MindMatchDbContext context)
    {
        _context = context;
    }

    public Task<Match?> GetByIdAsync(
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        return _context.Matches
            .SingleOrDefaultAsync(
                x => x.Id == matchId,
                cancellationToken);
    }

    public Task<bool> HasPendingOrAcceptedMatchAsync(
        Guid userId,
        Guid otherUserId,
        CancellationToken cancellationToken = default)
    {
        return _context.Matches.AnyAsync(
            x =>
                ((x.RequesterId == userId &&
                  x.ReceiverId == otherUserId) ||
                 (x.RequesterId == otherUserId &&
                  x.ReceiverId == userId)) &&
                (x.Status == MatchStatus.Pending ||
                 x.Status == MatchStatus.Accepted),
            cancellationToken);
    }

    public Task<bool> HasAcceptedMatchAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken = default) =>
        _context.Matches.AnyAsync(x =>
            ((x.RequesterId == userId && x.ReceiverId == otherUserId) ||
             (x.RequesterId == otherUserId && x.ReceiverId == userId)) &&
            x.Status == MatchStatus.Accepted, cancellationToken);

    public Task<List<Match>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _context.Matches
            .Where(x =>
                x.RequesterId == userId ||
                x.ReceiverId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Match>> GetIncomingPendingAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.Matches.Where(x => x.ReceiverId == userId && x.Status == MatchStatus.Pending &&
                _context.Users.Any(u => u.Id == x.RequesterId && u.IsActive) &&
                !_context.UserBlocks.Any(block =>
                    (block.BlockerId == x.RequesterId && block.BlockedUserId == x.ReceiverId) ||
                    (block.BlockerId == x.ReceiverId && block.BlockedUserId == x.RequesterId)))
            .OrderByDescending(x => x.CreatedAtUtc).Take(100).ToListAsync(cancellationToken);

    public async Task AddAsync(
        Match match,
        CancellationToken cancellationToken = default)
    {
        await _context.Matches.AddAsync(
            match, cancellationToken);
    }

    public void Update(Match match)
    {
        _context.Matches.Update(match);
    }
}
