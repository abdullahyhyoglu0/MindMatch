using MindMatch.Application.Dtos.Stories;

namespace MindMatch.Application.Interfaces;

public interface IStoryService
{
    Task<StoryResponse> CreateAsync(Guid userId, CreateStoryRequest request, CancellationToken cancellationToken = default);
    Task<List<StoryResponse>> GetFeedAsync(Guid userId, int take, CancellationToken cancellationToken = default);
    Task<StoryResponse> GetAsync(Guid viewerId, Guid storyId, CancellationToken cancellationToken = default);
    Task<List<StoryResponse>> GetUserStoriesAsync(Guid viewerId, Guid userId, CancellationToken cancellationToken = default);
    Task ViewAsync(Guid viewerId, Guid storyId, CancellationToken cancellationToken = default);
    Task<List<StoryViewerResponse>> GetViewersAsync(Guid userId, Guid storyId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid userId, Guid storyId, CancellationToken cancellationToken = default);
}
