using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;
using MindMatch.Infrastructure.Persistence;

namespace MindMatch.Infrastructure.Repositories;

public sealed class MediaAssetRepository : IMediaAssetRepository
{
    private readonly MindMatchDbContext _context;
    public MediaAssetRepository(MindMatchDbContext context) => _context = context;
    public Task<MediaAsset?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.MediaAssets.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task AddAsync(MediaAsset asset, CancellationToken cancellationToken = default) =>
        _context.MediaAssets.AddAsync(asset, cancellationToken).AsTask();
    public Task<bool> IsUsedInMessageAsync(Guid mediaAssetId, CancellationToken cancellationToken = default) =>
        _context.Messages.AnyAsync(x => x.MediaAssetId == mediaAssetId, cancellationToken);
    public async Task<bool> IsReferencedAsync(Guid mediaAssetId, CancellationToken cancellationToken = default) =>
        await _context.Messages.AnyAsync(x => x.MediaAssetId == mediaAssetId, cancellationToken) ||
        await _context.ProfilePhotos.AnyAsync(x => x.MediaAssetId == mediaAssetId, cancellationToken) ||
        await _context.Stories.AnyAsync(x => x.MediaAssetId == mediaAssetId, cancellationToken) ||
        await _context.ReportedMessages.AnyAsync(x => x.MediaAssetIdSnapshot == mediaAssetId &&
            x.EvidenceExpiresAtUtc > DateTime.UtcNow, cancellationToken) ||
        await _context.ReportedStories.AnyAsync(x => x.MediaAssetIdSnapshot == mediaAssetId &&
            x.EvidenceExpiresAtUtc > DateTime.UtcNow, cancellationToken);

    public Task<List<MediaAsset>> GetDeletedUnreferencedBatchAsync(int take, CancellationToken cancellationToken = default) =>
        _context.MediaAssets.Where(x => x.IsDeleted &&
            !_context.Messages.Any(m => m.MediaAssetId == x.Id) &&
            !_context.ProfilePhotos.Any(p => p.MediaAssetId == x.Id) &&
            !_context.Stories.Any(s => s.MediaAssetId == x.Id) &&
            !_context.ReportedMessages.Any(r => r.MediaAssetIdSnapshot == x.Id && r.EvidenceExpiresAtUtc > DateTime.UtcNow) &&
            !_context.ReportedStories.Any(r => r.MediaAssetIdSnapshot == x.Id && r.EvidenceExpiresAtUtc > DateTime.UtcNow))
            .OrderBy(x => x.CreatedAtUtc).Take(take).ToListAsync(cancellationToken);

    public Task<List<MediaAsset>> GetExpiredUnattachedBatchAsync(DateTime createdBeforeUtc, int take, CancellationToken cancellationToken = default) =>
        _context.MediaAssets.Where(x => !x.IsDeleted && x.CreatedAtUtc <= createdBeforeUtc &&
            !_context.Messages.Any(m => m.MediaAssetId == x.Id) &&
            !_context.ProfilePhotos.Any(p => p.MediaAssetId == x.Id) &&
            !_context.Stories.Any(s => s.MediaAssetId == x.Id) &&
            !_context.ReportedMessages.Any(r => r.MediaAssetIdSnapshot == x.Id && r.EvidenceExpiresAtUtc > DateTime.UtcNow) &&
            !_context.ReportedStories.Any(r => r.MediaAssetIdSnapshot == x.Id && r.EvidenceExpiresAtUtc > DateTime.UtcNow))
            .OrderBy(x => x.CreatedAtUtc).Take(take).ToListAsync(cancellationToken);

    public void Remove(MediaAsset asset) => _context.MediaAssets.Remove(asset);
}
