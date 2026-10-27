using MindMatch.Application.Dtos.Stories;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Services;

public sealed class StoryService : IStoryService
{
    private readonly IStoryRepository _stories;
    private readonly IUserRepository _users;
    private readonly IUserProfileRepository _profiles;
    private readonly IUserBlockRepository _blocks;
    private readonly IMediaAssetRepository _assets;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserFollowRepository _follows;
    private readonly IUserPresenceReader _presence;

    public StoryService(IStoryRepository stories, IUserRepository users, IUserProfileRepository profiles,
        IUserBlockRepository blocks, IMediaAssetRepository assets, IUnitOfWork unitOfWork,
        IUserFollowRepository follows, IUserPresenceReader presence)
    { _stories = stories; _users = users; _profiles = profiles; _blocks = blocks; _assets = assets;
      _unitOfWork = unitOfWork; _follows = follows; _presence = presence; }

    public StoryService(IStoryRepository stories, IUserRepository users, IUserProfileRepository profiles,
        IUserBlockRepository blocks, IMediaAssetRepository assets, IUnitOfWork unitOfWork)
        : this(stories, users, profiles, blocks, assets, unitOfWork, null!, null!) { }

    public async Task<List<StoryTrayItemResponse>> GetTrayAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (_follows is null || _presence is null)
            throw new InvalidOperationException("Hikâye tepsisi bağımlılıkları yapılandırılmamış.");
        if (userId == Guid.Empty) throw new ArgumentException("Kullanıcı kimliği geçersiz.");
        var followed = await _follows.GetAcceptedFollowedIdsAsync(userId, cancellationToken);
        var ids = followed.Append(userId).Distinct().ToArray();
        var blocked = await _blocks.GetRelatedUserIdsAsync(userId, cancellationToken);
        var stories = await _stories.GetFeedAsync(userId, blocked, 500, cancellationToken);
        var ownStories = await _stories.GetByUserIdAsync(userId, cancellationToken);
        stories.AddRange(ownStories);
        stories = stories.Where(x => ids.Contains(x.UserId)).ToList();
        var viewedIds = await _stories.GetViewedStoryIdsAsync(userId, stories.Select(x => x.Id).ToArray(), cancellationToken);
        var viewed = viewedIds.ToHashSet();
        var statuses = await _presence.GetStatusesAsync(ids, cancellationToken);
        var profiles = (await _profiles.GetByUserIdsAsync(ids, cancellationToken)).ToDictionary(x => x.UserId);
        var trays = stories.GroupBy(x => x.UserId).Select(group =>
        {
            var isSelf = group.Key == userId;
            var groupedStories = group.OrderBy(x => x.CreatedAtUtc).Select(x => Map(x, viewed.Contains(x.Id))).ToList();
            var status = statuses.GetValueOrDefault(group.Key);
            var profile = profiles.GetValueOrDefault(group.Key);
            return new StoryTrayItemResponse
            {
                UserId = group.Key, IsSelf = isSelf, HasActiveStory = true,
                HasUnseenStory = groupedStories.Any(x => !x.HasViewed),
                IsOnline = profile?.ShowOnlineStatus == true ? status.IsOnline : null,
                LastSeenAtUtc = profile?.ShowLastSeen == true && profile.ShowOnlineStatus
                    ? status.LastSeenAtUtc : null,
                Stories = groupedStories
            };
        }).ToList();
        foreach (var followedId in followed.Where(id => trays.All(x => x.UserId != id)))
        {
            var profile = profiles.GetValueOrDefault(followedId);
            var status = statuses.GetValueOrDefault(followedId);
            trays.Add(new StoryTrayItemResponse
            {
                UserId = followedId,
                IsOnline = profile?.ShowOnlineStatus == true ? status.IsOnline : null,
                LastSeenAtUtc = profile?.ShowLastSeen == true && profile.ShowOnlineStatus ? status.LastSeenAtUtc : null
            });
        }
        if (trays.All(x => !x.IsSelf))
        {
            var profile = profiles.GetValueOrDefault(userId);
            var status = statuses.GetValueOrDefault(userId);
            trays.Add(new StoryTrayItemResponse { UserId = userId, IsSelf = true,
                IsOnline = profile?.ShowOnlineStatus == true ? status.IsOnline : null,
                LastSeenAtUtc = profile?.ShowLastSeen == true && profile.ShowOnlineStatus ? status.LastSeenAtUtc : null });
        }
        return trays.OrderByDescending(x => x.IsSelf).ThenByDescending(x => x.HasActiveStory)
            .ThenByDescending(x => x.IsOnline == true).ToList();
    }

    public async Task<StoryResponse> CreateAsync(Guid userId, CreateStoryRequest request, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || request.MediaAssetId == Guid.Empty) throw new ArgumentException("Hikâye bilgileri geçersiz.");
        var user = await _users.GetByIdAsync(userId, cancellationToken) ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");
        if (!user.IsActive) throw new UnauthorizedAccessException("Etkin olmayan hesap hikâye paylaşamaz.");
        var category = request.MediaType switch
        {
            StoryMediaType.Image => MediaCategory.StoryImage,
            StoryMediaType.Video => MediaCategory.StoryVideo,
            _ => throw new ArgumentException("Hikâye türü geçersiz.")
        };
        var asset = await _assets.GetByIdAsync(request.MediaAssetId, cancellationToken);
        if (asset is null || asset.OwnerId != userId || asset.IsDeleted || asset.Category != category)
            throw new UnauthorizedAccessException("Bu medya hikâyede kullanılamaz.");
        if (await _assets.IsReferencedAsync(asset.Id, cancellationToken))
            throw new InvalidOperationException("Bu medya başka içerikte kullanılmış.");
        var story = new Story(userId, request.MediaType, asset.Id, request.Caption);
        await _stories.AddAsync(story, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(story, false);
    }

    public async Task<List<StoryResponse>> GetFeedAsync(Guid userId, int take, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || take is < 1 or > 100) throw new ArgumentException("Akış isteği geçersiz.");
        var blocked = await _blocks.GetRelatedUserIdsAsync(userId, cancellationToken);
        var excluded = blocked.Append(userId).Distinct().ToArray();
        var stories = await _stories.GetFeedAsync(userId, excluded, take, cancellationToken);
        var viewedIds = (await _stories.GetViewedStoryIdsAsync(userId,
            stories.Select(x => x.Id).ToArray(), cancellationToken)).ToHashSet();
        return stories.Select(x => Map(x, viewedIds.Contains(x.Id))).ToList();
    }

    public async Task<StoryResponse> GetAsync(Guid viewerId, Guid storyId, CancellationToken cancellationToken = default)
    {
        var story = await GetAccessibleStory(viewerId, storyId, cancellationToken);
        var hasViewed = story.UserId != viewerId && await _stories.HasViewAsync(storyId, viewerId, cancellationToken);
        return Map(story, hasViewed);
    }

    public async Task<List<StoryResponse>> GetUserStoriesAsync(Guid viewerId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (viewerId == Guid.Empty || userId == Guid.Empty) throw new ArgumentException("Kullanıcı kimliği geçersiz.");
        if (viewerId != userId)
        {
            var profile = await _profiles.GetByUserIdAsync(userId, cancellationToken);
            var owner = await _users.GetByIdAsync(userId, cancellationToken);
            if (profile is null || owner is null || !owner.IsActive ||
                !(await SocialAccess.CanViewProfileAsync(viewerId, profile, _follows, cancellationToken)) ||
                await _blocks.IsBlockedEitherWayAsync(viewerId, userId, cancellationToken))
                throw new KeyNotFoundException("Hikâye bulunamadı.");
        }
        var stories = await _stories.GetByUserIdAsync(userId, cancellationToken);
        var viewedIds = viewerId == userId || stories.Count == 0
            ? []
            : (await _stories.GetViewedStoryIdsAsync(viewerId,
                stories.Select(x => x.Id).ToArray(), cancellationToken)).ToHashSet();
        return stories.Select(story => Map(story, viewedIds.Contains(story.Id))).ToList();
    }

    public async Task ViewAsync(Guid viewerId, Guid storyId, CancellationToken cancellationToken = default)
    {
        var story = await GetAccessibleStory(viewerId, storyId, cancellationToken);
        if (story.UserId == viewerId) throw new InvalidOperationException("Kendi hikâyenizi görüntüleme olarak kaydedemezsiniz.");
        if (await _stories.HasViewAsync(storyId, viewerId, cancellationToken)) return;
        await _stories.AddViewAsync(new StoryView(storyId, viewerId), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<StoryViewerResponse>> GetViewersAsync(Guid userId, Guid storyId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || storyId == Guid.Empty) throw new ArgumentException("Kullanıcı ve hikâye kimlikleri gereklidir.");
        var story = await _stories.GetByIdAsync(storyId, cancellationToken)
            ?? throw new KeyNotFoundException("Hikâye bulunamadı.");
        if (story.UserId != userId) throw new UnauthorizedAccessException("Bu hikâyenin görüntüleyenlerini yalnızca sahibi görebilir.");
        if (story.IsDeleted || story.IsExpired()) throw new KeyNotFoundException("Hikâye bulunamadı.");
        return (await _stories.GetViewsAsync(storyId, cancellationToken))
            .Select(view => new StoryViewerResponse(view.ViewerId, view.ViewedAtUtc)).ToList();
    }

    public async Task DeleteAsync(Guid userId, Guid storyId, CancellationToken cancellationToken = default)
    {
        var story = await _stories.GetByIdAsync(storyId, cancellationToken) ?? throw new KeyNotFoundException("Hikâye bulunamadı.");
        if (story.UserId != userId) throw new UnauthorizedAccessException("Bu hikâye size ait değil.");
        if (story.MediaAssetId.HasValue)
        {
            var asset = await _assets.GetByIdAsync(story.MediaAssetId.Value, cancellationToken);
            asset?.MarkDeleted();
        }
        story.Delete();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Story> GetAccessibleStory(Guid viewerId, Guid storyId, CancellationToken cancellationToken)
    {
        if (viewerId == Guid.Empty || storyId == Guid.Empty) throw new ArgumentException("Kimlik geçersiz.");
        var story = await _stories.GetByIdAsync(storyId, cancellationToken) ?? throw new KeyNotFoundException("Hikâye bulunamadı.");
        if (story.IsDeleted || story.IsExpired()) throw new KeyNotFoundException("Hikâye bulunamadı.");
        if (story.UserId != viewerId)
        {
            var owner = await _users.GetByIdAsync(story.UserId, cancellationToken);
            var profile = await _profiles.GetByUserIdAsync(story.UserId, cancellationToken);
            if (owner is null || !owner.IsActive || profile is null ||
                !(await SocialAccess.CanViewProfileAsync(viewerId, profile, _follows, cancellationToken)) ||
                await _blocks.IsBlockedEitherWayAsync(viewerId, story.UserId, cancellationToken))
                throw new KeyNotFoundException("Hikâye bulunamadı.");
        }
        return story;
    }

    private static StoryResponse Map(Story x, bool hasViewed) => new()
    { Id = x.Id, UserId = x.UserId, MediaAssetId = x.MediaAssetId, MediaType = x.MediaType,
      Caption = x.Caption, CreatedAtUtc = x.CreatedAtUtc, ExpiresAtUtc = x.ExpiresAtUtc, HasViewed = hasViewed };
}
