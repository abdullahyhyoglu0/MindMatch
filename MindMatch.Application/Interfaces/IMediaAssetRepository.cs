using MindMatch.Domain.Entities;

namespace MindMatch.Application.Interfaces;

public interface IMediaAssetRepository
{
    Task<MediaAsset?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(MediaAsset asset, CancellationToken cancellationToken = default);
    Task<bool> IsUsedInMessageAsync(Guid mediaAssetId, CancellationToken cancellationToken = default);
    Task<bool> IsReferencedAsync(Guid mediaAssetId, CancellationToken cancellationToken = default);
    Task<List<MediaAsset>> GetDeletedUnreferencedBatchAsync(int take, CancellationToken cancellationToken = default);
    Task<List<MediaAsset>> GetExpiredUnattachedBatchAsync(DateTime createdBeforeUtc, int take, CancellationToken cancellationToken = default);
    void Remove(MediaAsset asset);
}
