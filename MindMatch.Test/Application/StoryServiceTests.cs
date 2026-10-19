using MindMatch.Application.Interfaces;
using MindMatch.Application.Services;
using MindMatch.Domain.Entities;
using System.Reflection;

namespace MindMatch.Test.Application;

public sealed class StoryServiceTests
{
    [Fact]
    public async Task GetStory_OnlyReturnsItWhenOwnerIsVisibleAndUnblocked()
    {
        var owner = new User("owner@example.com", "hash");
        var viewer = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var story = new Story(owner.Id, StoryMediaType.Image, assetId);
        var service = CreateService(story, owner, blocked: false);

        var response = await service.GetAsync(viewer, story.Id);

        Assert.Equal(assetId, response.MediaAssetId);
        Assert.True(response.HasViewed);
    }

    [Fact]
    public async Task GetStory_HidesItFromBlockedViewer()
    {
        var owner = new User("owner@example.com", "hash");
        var story = new Story(owner.Id, StoryMediaType.Image, Guid.NewGuid());
        var service = CreateService(story, owner, blocked: true);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAsync(Guid.NewGuid(), story.Id));
    }

    [Fact]
    public async Task GetViewers_RejectsDeletedStory()
    {
        var owner = new User("owner@example.com", "hash");
        var story = new Story(owner.Id, StoryMediaType.Image, Guid.NewGuid());
        story.Delete();
        var service = CreateService(story, owner, blocked: false);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetViewersAsync(owner.Id, story.Id));
    }

    [Fact]
    public async Task GetViewers_RejectsExpiredStory()
    {
        var owner = new User("owner@example.com", "hash");
        var story = new Story(owner.Id, StoryMediaType.Image, Guid.NewGuid());
        typeof(Story).GetProperty(nameof(Story.ExpiresAtUtc), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(story, DateTime.UtcNow.AddMinutes(-1));
        var service = CreateService(story, owner, blocked: false);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetViewersAsync(owner.Id, story.Id));
    }

    [Fact]
    public async Task GetFeed_ReportsViewedStateForEachStory()
    {
        var owner = new User("owner@example.com", "hash");
        var viewerId = Guid.NewGuid();
        var story = new Story(owner.Id, StoryMediaType.Image, Guid.NewGuid());
        var storyRepository = new FakeStoryRepository(story)
        {
            FeedStories = [story],
            ViewedStoryIds = [story.Id]
        };
        var service = CreateService(story, owner, blocked: false, storyRepository);

        var response = await service.GetFeedAsync(viewerId, 20);

        Assert.True(Assert.Single(response).HasViewed);
    }

    private static StoryService CreateService(Story story, User owner, bool blocked,
        FakeStoryRepository? storyRepository = null)
    {
        var profile = new UserProfile(owner.Id, "Owner");
        profile.SetProfileVisibility(true);
        return new StoryService(storyRepository ?? new FakeStoryRepository(story), new FakeUserRepository(owner),
            new FakeProfileRepository(profile), new FakeBlockRepository(blocked),
            new FakeMediaRepository(), new FakeUnitOfWork());
    }

    private sealed class FakeStoryRepository(Story story) : IStoryRepository
    {
        public List<Story> FeedStories { get; init; } = [];
        public List<Guid> ViewedStoryIds { get; init; } = [];
        public Task<Story?> GetByIdAsync(Guid storyId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Story?>(story.Id == storyId ? story : null);
        public Task<Story?> GetByMediaAssetIdAsync(Guid mediaAssetId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Story?>(null);
        public Task<List<Story>> GetFeedAsync(Guid viewerId, IReadOnlyCollection<Guid> excludedUserIds, int take, CancellationToken cancellationToken = default) => Task.FromResult(FeedStories.Take(take).ToList());
        public Task<List<Story>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(FeedStories.Count > 0 ? FeedStories : new List<Story>());
        public Task AddAsync(Story item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddViewAsync(StoryView view, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> HasViewAsync(Guid storyId, Guid viewerId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<List<Guid>> GetViewedStoryIdsAsync(Guid viewerId, IReadOnlyCollection<Guid> storyIds, CancellationToken cancellationToken = default) => Task.FromResult(ViewedStoryIds.Intersect(storyIds).ToList());
        public Task<List<StoryView>> GetViewsAsync(Guid storyId, CancellationToken cancellationToken = default) => Task.FromResult(new List<StoryView>());
        public Task<List<Story>> GetExpiredBatchAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default) => Task.FromResult(new List<Story>());
    }

    private sealed class FakeUserRepository(User owner) : IUserRepository
    {
        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult<User?>(null);
        public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<User?>(userId == owner.Id ? owner : null);
        public Task AddAsync(User item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<List<User>> GetExpiredAdminFrozenBatchAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default) => Task.FromResult(new List<User>());
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeProfileRepository(UserProfile profile) : IUserProfileRepository
    {
        public Task<UserProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<UserProfile?>(userId == profile.UserId ? profile : null);
        public Task AddAsync(UserProfile item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(userId == profile.UserId);
        public Task<bool> IsNicknameTakenAsync(string normalizedNickname, Guid? exceptUserId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<List<UserProfile>> DiscoverAsync(Guid viewerId, Gender viewerGender, GenderPreference targetGenderPreference, int? viewerAge, int? minimumAge, int? maximumAge, string? city, IReadOnlyCollection<Guid> excludedUserIds, bool premiumOnly, int skip, int take, CancellationToken cancellationToken = default) => Task.FromResult(new List<UserProfile>());
        public Task<List<Guid>> GetRecentlySeenOrRejectedUserIdsAsync(Guid viewerId, DateTime seenSinceUtc, CancellationToken cancellationToken = default) => Task.FromResult(new List<Guid>());
        public Task RecordDiscoveryDecisionAsync(Guid viewerId, Guid targetUserId, bool rejected, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeBlockRepository(bool blocked) : IUserBlockRepository
    {
        public Task<bool> IsBlockedEitherWayAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken = default) => Task.FromResult(blocked);
        public Task<bool> IsBlockedByAsync(Guid blockerId, Guid otherUserId, CancellationToken cancellationToken = default) => Task.FromResult(blocked);
        public Task<List<Guid>> GetBlockedUserIdsAsync(Guid blockerId, CancellationToken cancellationToken = default) => Task.FromResult(new List<Guid>());
        public Task<List<Guid>> GetRelatedUserIdsAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(new List<Guid>());
        public Task AddAsync(UserBlock block, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(Guid blockerId, Guid blockedUserId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeMediaRepository : IMediaAssetRepository
    {
        public Task<MediaAsset?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<MediaAsset?>(null);
        public Task AddAsync(MediaAsset asset, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> IsUsedInMessageAsync(Guid mediaAssetId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> IsReferencedAsync(Guid mediaAssetId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<List<MediaAsset>> GetDeletedUnreferencedBatchAsync(int take, CancellationToken cancellationToken = default) => Task.FromResult(new List<MediaAsset>());
        public Task<List<MediaAsset>> GetExpiredUnattachedBatchAsync(DateTime createdBeforeUtc, int take, CancellationToken cancellationToken = default) => Task.FromResult(new List<MediaAsset>());
        public void Remove(MediaAsset asset) { }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }
}
