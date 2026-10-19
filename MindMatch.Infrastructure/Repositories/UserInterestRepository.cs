
using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;
using MindMatch.Infrastructure.Persistence;

namespace MindMatch.Infrastructure.Repositories;

public class UserInterestRepository : IUserInterestRepository
{
    private readonly MindMatchDbContext _context;

    public UserInterestRepository(MindMatchDbContext context)
    {
        _context = context;
    }

    public async Task<List<Guid>> GetInterestIdsByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.UserInterests
            .AsNoTracking()
            .Where(ui => ui.UserId == userId)
            .Select(ui => ui.InterestId)
            .ToListAsync(cancellationToken);
    }

    public Task<List<UserInterest>> GetByUserIdsAsync(IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default) =>
        _context.UserInterests.AsNoTracking().Where(x => userIds.Contains(x.UserId)).ToListAsync(cancellationToken);

    public async Task<bool> ReplaceForUserAsync(Guid userId, IReadOnlyCollection<Guid> interestIds,
        CancellationToken cancellationToken = default)
    {
        var requested = interestIds.Distinct().ToHashSet();
        var validCount = await _context.Interests.CountAsync(x => requested.Contains(x.Id), cancellationToken);
        if (validCount != requested.Count) return false;
        var current = await _context.UserInterests.Where(x => x.UserId == userId).ToListAsync(cancellationToken);
        _context.UserInterests.RemoveRange(current.Where(x => !requested.Contains(x.InterestId)));
        var currentIds = current.Select(x => x.InterestId).ToHashSet();
        foreach (var interestId in requested.Where(x => !currentIds.Contains(x)))
            await _context.UserInterests.AddAsync(new UserInterest(userId, interestId), cancellationToken);
        return true;
    }

    public async Task<bool> ExistsAsync(
        Guid userId,
        Guid interestId,
        CancellationToken cancellationToken = default)
    {
        return await _context.UserInterests
            .AnyAsync(
                ui => ui.UserId == userId &&
                      ui.InterestId == interestId,
                cancellationToken);
    }

    public async Task AddAsync(
        UserInterest userInterest,
        CancellationToken cancellationToken = default)
    {
        await _context.UserInterests.AddAsync(
            userInterest,
            cancellationToken);
    }

    public async Task RemoveAsync(
        Guid userId,
        Guid interestId,
        CancellationToken cancellationToken = default)
    {
        var userInterest = await _context.UserInterests
            .FirstOrDefaultAsync(
                ui => ui.UserId == userId &&
                      ui.InterestId == interestId,
                cancellationToken);

        if (userInterest is not null)
        {
            _context.UserInterests.Remove(userInterest);
        }
    }
}
