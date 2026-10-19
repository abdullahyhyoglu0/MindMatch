using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;
using MindMatch.Infrastructure.Persistence;

namespace MindMatch.Infrastructure.Repositories;

public sealed class StoryRepository : IStoryRepository
{
    private readonly MindMatchDbContext _context;
    public StoryRepository(MindMatchDbContext context) => _context = context;

    public Task<Story?> GetByIdAsync(Guid storyId, CancellationToken cancellationToken = default) =>
        _context.Stories.SingleOrDefaultAsync(x => x.Id == storyId, cancellationToken);

    public Task<Story?> GetByMediaAssetIdAsync(Guid mediaAssetId, CancellationToken cancellationToken = default) =>
        _context.Stories.FirstOrDefaultAsync(x => x.MediaAssetId == mediaAssetId && !x.IsDeleted && x.ExpiresAtUtc > DateTime.UtcNow, cancellationToken);

    public Task<List<Story>> GetFeedAsync(Guid viewerId, IReadOnlyCollection<Guid> excludedUserIds, int take, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return _context.Stories.Where(x => x.UserId != viewerId && !x.IsDeleted && x.ExpiresAtUtc > now &&
                !excludedUserIds.Contains(x.UserId) &&
                _context.UserProfiles.Any(p => p.UserId == x.UserId && p.IsProfileVisible) &&
                _context.Users.Any(u => u.Id == x.UserId && u.IsActive))
            .OrderByDescending(x => x.CreatedAtUtc).Take(take).ToListAsync(cancellationToken);
    }

    public Task<List<Story>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.Stories.Where(x => x.UserId == userId && !x.IsDeleted && x.ExpiresAtUtc > DateTime.UtcNow)
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);

    public Task AddAsync(Story story, CancellationToken cancellationToken = default) =>
        _context.Stories.AddAsync(story, cancellationToken).AsTask();

    public async Task AddViewAsync(StoryView view, CancellationToken cancellationToken = default)
    {
        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO story_views ("Id", "StoryId", "ViewerId", "ViewedAtUtc", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ({view.Id}, {view.StoryId}, {view.ViewerId}, {view.ViewedAtUtc}, {view.CreatedAtUtc}, {view.UpdatedAtUtc})
            ON CONFLICT ("StoryId", "ViewerId") DO NOTHING
            """, cancellationToken);
    }

    public Task<bool> HasViewAsync(Guid storyId, Guid viewerId, CancellationToken cancellationToken = default) =>
        _context.StoryViews.AnyAsync(x => x.StoryId == storyId && x.ViewerId == viewerId, cancellationToken);

    public Task<List<Guid>> GetViewedStoryIdsAsync(Guid viewerId, IReadOnlyCollection<Guid> storyIds,
        CancellationToken cancellationToken = default)
    {
        if (storyIds.Count == 0) return Task.FromResult(new List<Guid>());
        return _context.StoryViews.AsNoTracking()
            .Where(x => x.ViewerId == viewerId && storyIds.Contains(x.StoryId))
            .Select(x => x.StoryId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public Task<List<StoryView>> GetViewsAsync(Guid storyId, CancellationToken cancellationToken = default) =>
        _context.StoryViews.AsNoTracking().Where(x => x.StoryId == storyId)
            .OrderByDescending(x => x.ViewedAtUtc).ToListAsync(cancellationToken);

    public Task<List<Story>> GetExpiredBatchAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default) =>
        _context.Stories.Where(x => !x.IsDeleted && x.ExpiresAtUtc <= nowUtc)
            .OrderBy(x => x.ExpiresAtUtc).Take(take).ToListAsync(cancellationToken);
}
