
using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;
using MindMatch.Infrastructure.Persistence;

namespace MindMatch.Infrastructure.Repositories;

public class MoodCheckInRepository : IMoodCheckInRepository
{
    private readonly MindMatchDbContext _context;

    public MoodCheckInRepository(MindMatchDbContext context)
    {
        _context = context;
    }

    public Task<MoodCheckIn?> GetLatestByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _context.MoodCheckIns
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<List<MoodCheckIn>> GetLatestActiveByUserIdsAsync(IReadOnlyCollection<Guid> userIds,
        DateTime nowUtc, CancellationToken cancellationToken = default) =>
        _context.MoodCheckIns.AsNoTracking()
            .Where(x => userIds.Contains(x.UserId) && x.ExpiresAtUtc > nowUtc)
            .GroupBy(x => x.UserId)
            .Select(group => group.OrderByDescending(x => x.CreatedAtUtc).First())
            .ToListAsync(cancellationToken);

    public async Task AddAsync(
        MoodCheckIn moodCheckIn,
        CancellationToken cancellationToken = default)
    {
        await _context.MoodCheckIns.AddAsync(
            moodCheckIn, cancellationToken);
    }
}
