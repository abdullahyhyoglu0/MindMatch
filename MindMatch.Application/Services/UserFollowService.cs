using MindMatch.Application.Dtos.Follow;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Services;

public sealed class UserFollowService : IUserFollowService
{
    private readonly IUserFollowRepository _follows;
    private readonly IUserRepository _users;
    private readonly IUserProfileRepository _profiles;
    private readonly IUserBlockRepository _blocks;
    private readonly IRealtimeOutboxRepository _outbox;
    private readonly INotificationService _notifications;
    private readonly IUnitOfWork _unitOfWork;

    public UserFollowService(IUserFollowRepository follows, IUserRepository users, IUserProfileRepository profiles,
        IUserBlockRepository blocks, IRealtimeOutboxRepository outbox, INotificationService notifications,
        IUnitOfWork unitOfWork)
    {
        _follows = follows;
        _users = users;
        _profiles = profiles;
        _blocks = blocks;
        _outbox = outbox;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
    }

    public async Task<FollowResponse> FollowAsync(Guid followerId, Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        ValidatePair(followerId, targetUserId);
        var target = await RequireActiveUserAsync(targetUserId, cancellationToken);
        if (await _blocks.IsBlockedEitherWayAsync(followerId, targetUserId, cancellationToken))
            throw new KeyNotFoundException("Kullanıcı bulunamadı.");
        var profile = await _profiles.GetByUserIdAsync(targetUserId, cancellationToken)
            ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");

        var existing = await _follows.GetAsync(followerId, targetUserId, cancellationToken);
        if (existing is not null)
            return await MapAsync(existing, followerId, cancellationToken);

        var follow = new UserFollow(followerId, targetUserId, profile.IsProfileVisible);
        await _follows.AddAsync(follow, cancellationToken);
        if (follow.Status == FollowStatus.Pending)
        {
            await _outbox.AddAsync(RealtimeEventOutbox.ToUser(targetUserId, "FollowRequestReceived",
                new { FollowerId = followerId, follow.Id }), cancellationToken);
            await _notifications.QueueAsync(targetUserId, NotificationType.FollowRequestReceived,
                "Yeni takip isteği", "Bir kullanıcı sizi takip etmek istiyor.", followerId, cancellationToken);
        }
        else
        {
            await _outbox.AddAsync(RealtimeEventOutbox.ToUser(targetUserId, "NewFollower",
                new { FollowerId = followerId, follow.Id }), cancellationToken);
            await _notifications.QueueAsync(targetUserId, NotificationType.NewFollower,
                "Yeni takipçi", "Bir kullanıcı sizi takip etmeye başladı.", followerId, cancellationToken);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapAsync(follow, followerId, cancellationToken);
    }

    public async Task UnfollowAsync(Guid followerId, Guid targetUserId, CancellationToken cancellationToken = default)
    {
        ValidatePair(followerId, targetUserId);
        var follow = await _follows.GetAsync(followerId, targetUserId, cancellationToken);
        if (follow is null) return;
        await _follows.RemoveAsync(follow, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveFollowerAsync(Guid userId, Guid followerId, CancellationToken cancellationToken = default)
    {
        ValidatePair(userId, followerId);
        var follow = await _follows.GetAsync(followerId, userId, cancellationToken);
        if (follow is null) return;
        await _follows.RemoveAsync(follow, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task AcceptAsync(Guid userId, Guid followerId, CancellationToken cancellationToken = default)
    {
        ValidatePair(userId, followerId);
        if (await _blocks.IsBlockedEitherWayAsync(userId, followerId, cancellationToken))
            throw new KeyNotFoundException("Takip isteği bulunamadı.");
        var follow = await _follows.GetAsync(followerId, userId, cancellationToken)
            ?? throw new KeyNotFoundException("Takip isteği bulunamadı.");
        follow.Accept(userId);
        await _outbox.AddAsync(RealtimeEventOutbox.ToUser(followerId, "FollowAccepted",
            new { FollowedId = userId, follow.Id }), cancellationToken);
        await _notifications.QueueAsync(followerId, NotificationType.FollowAccepted,
            "Takip isteği kabul edildi", "Takip isteğiniz kabul edildi.", userId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RejectAsync(Guid userId, Guid followerId, CancellationToken cancellationToken = default)
    {
        ValidatePair(userId, followerId);
        var follow = await _follows.GetAsync(followerId, userId, cancellationToken)
            ?? throw new KeyNotFoundException("Takip isteği bulunamadı.");
        follow.EnsurePending();
        if (follow.FollowedId != userId)
            throw new UnauthorizedAccessException("Takip isteğini yalnızca alıcı yanıtlayabilir.");
        await _follows.RemoveAsync(follow, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<FollowResponse>> GetIncomingAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) throw new ArgumentException("Kullanıcı kimliği geçersiz.");
        var requests = await _follows.GetIncomingPendingAsync(userId, cancellationToken);
        var visibleRequests = new List<UserFollow>(requests.Count);
        foreach (var request in requests)
        {
            if (!await _blocks.IsBlockedEitherWayAsync(userId, request.FollowerId, cancellationToken))
                visibleRequests.Add(request);
        }
        var result = new List<FollowResponse>(visibleRequests.Count);
        foreach (var request in visibleRequests)
            result.Add(await MapAsync(request, userId, cancellationToken));
        return result;
    }

    public Task<FollowListResponse> GetFollowersAsync(Guid viewerId, Guid targetUserId, int page, int pageSize,
        CancellationToken cancellationToken = default) =>
        GetListAsync(viewerId, targetUserId, page, pageSize, following: false, cancellationToken);

    public Task<FollowListResponse> GetFollowingAsync(Guid viewerId, Guid targetUserId, int page, int pageSize,
        CancellationToken cancellationToken = default) =>
        GetListAsync(viewerId, targetUserId, page, pageSize, following: true, cancellationToken);

    public async Task<FollowRelation> GetRelationAsync(Guid viewerId, Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        if (viewerId == Guid.Empty || targetUserId == Guid.Empty || viewerId == targetUserId)
            return FollowRelation.None;
        var pair = await _follows.GetPairAsync(viewerId, targetUserId, cancellationToken);
        return UserFollow.RelationOf(pair.Outgoing, pair.Incoming);
    }

    private async Task<FollowListResponse> GetListAsync(Guid viewerId, Guid targetUserId, int page, int pageSize,
        bool following, CancellationToken cancellationToken)
    {
        if (viewerId == Guid.Empty || targetUserId == Guid.Empty)
            throw new ArgumentException("Kullanıcı kimliği geçersiz.");
        if (page < 1 || pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
        if (await _blocks.IsBlockedEitherWayAsync(viewerId, targetUserId, cancellationToken))
            throw new KeyNotFoundException("Liste bulunamadı.");

        var profile = await _profiles.GetByUserIdAsync(targetUserId, cancellationToken)
            ?? throw new KeyNotFoundException("Liste bulunamadı.");
        var canView = await SocialAccess.CanViewProfileAsync(viewerId, profile, _follows, cancellationToken);
        var listsVisible = viewerId == targetUserId ||
            (canView && (following ? profile.ShowFollowing : profile.ShowFollowers));
        var count = following
            ? await _follows.CountAcceptedFollowingAsync(targetUserId, cancellationToken)
            : await _follows.CountAcceptedFollowersAsync(targetUserId, cancellationToken);
        if (!listsVisible)
            return new FollowListResponse { Page = page, PageSize = pageSize, IsHidden = true, Count = 0 };

        var skip = (page - 1) * pageSize;
        var rows = following
            ? await _follows.GetAcceptedFollowingAsync(targetUserId, skip, pageSize + 1, cancellationToken)
            : await _follows.GetAcceptedFollowersAsync(targetUserId, skip, pageSize + 1, cancellationToken);
        var hasMore = rows.Count > pageSize;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        var items = new List<FollowResponse>(rows.Count);
        foreach (var row in rows)
            items.Add(await MapAsync(row, viewerId, cancellationToken));
        return new FollowListResponse
        {
            Items = items, Page = page, PageSize = pageSize, HasMore = hasMore, Count = count
        };
    }

    private async Task<FollowResponse> MapAsync(UserFollow follow, Guid viewerId, CancellationToken cancellationToken)
    {
        var otherId = follow.FollowerId == viewerId ? follow.FollowedId : follow.FollowerId;
        if (follow.FollowerId != viewerId && follow.FollowedId != viewerId)
            otherId = follow.FollowerId;
        var profile = await _profiles.GetByUserIdAsync(otherId, cancellationToken);
        var pair = await _follows.GetPairAsync(viewerId, otherId, cancellationToken);
        return new FollowResponse
        {
            UserId = otherId,
            DisplayName = profile?.DisplayName ?? string.Empty,
            Nickname = profile?.Nickname ?? string.Empty,
            Status = follow.Status,
            Relation = UserFollow.RelationOf(pair.Outgoing, pair.Incoming),
            CreatedAtUtc = follow.CreatedAtUtc
        };
    }

    private async Task<User> RequireActiveUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken)
            ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");
        if (!user.IsActive) throw new KeyNotFoundException("Kullanıcı bulunamadı.");
        return user;
    }

    private static void ValidatePair(Guid left, Guid right)
    {
        if (left == Guid.Empty || right == Guid.Empty)
            throw new ArgumentException("Kullanıcı kimlikleri gereklidir.");
        if (left == right)
            throw new ArgumentException("İşlem aynı kullanıcıya uygulanamaz.");
    }
}
