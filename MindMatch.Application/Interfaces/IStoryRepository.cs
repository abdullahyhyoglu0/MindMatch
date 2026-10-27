using MindMatch.Domain.Entities;

namespace MindMatch.Application.Interfaces;

public interface IStoryRepository
{
    Task<Story?> GetByIdAsync(Guid storyId, CancellationToken cancellationToken = default);
    Task<Story?> GetByMediaAssetIdAsync(Guid mediaAssetId, CancellationToken cancellationToken = default);
    Task<List<Story>> GetFeedAsync(Guid viewerId, IReadOnlyCollection<Guid> excludedUserIds, int take, CancellationToken cancellationToken = default);
    Task<List<Story>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(Story story, CancellationToken cancellationToken = default);
    Task AddViewAsync(StoryView view, CancellationToken cancellationToken = default);
    Task<bool> HasViewAsync(Guid storyId, Guid viewerId, CancellationToken cancellationToken = default);
    Task<List<Guid>> GetViewedStoryIdsAsync(Guid viewerId, IReadOnlyCollection<Guid> storyIds,
        CancellationToken cancellationToken = default);
    Task<List<StoryView>> GetViewsAsync(Guid storyId, CancellationToken cancellationToken = default);
    Task<List<Story>> GetExpiredBatchAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default);
    Task<List<Guid>> GetActiveStoryOwnerIdsAsync(IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<Guid>());
    Task<List<Story>> GetLatestActiveByUserIdsAsync(IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<Story>());
}
