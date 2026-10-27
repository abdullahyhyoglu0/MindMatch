using MindMatch.Application.Dtos.UserProfile;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Services;

public sealed class DiscoveryService : IDiscoveryService
{
    private readonly IUserProfileRepository _profiles;
    private readonly IUserInterestRepository _interests;
    private readonly IUserBlockRepository _blocks;
    private readonly IPremiumService _premium;
    private readonly IUserMusicPreferenceRepository _music;
    private readonly IMoodCheckInRepository _moods;
    private readonly IUserFollowRepository _follows;
    private readonly IStoryRepository _stories;
    private readonly IUserPresenceReader _presence;

    public DiscoveryService(IUserProfileRepository profiles, IUserInterestRepository interests,
        IUserBlockRepository blocks, IPremiumService premium, IUserMusicPreferenceRepository music,
        IMoodCheckInRepository moods, IUserFollowRepository follows, IStoryRepository stories,
        IUserPresenceReader presence)
    {
        _profiles = profiles;
        _interests = interests;
        _blocks = blocks;
        _premium = premium;
        _music = music;
        _moods = moods;
        _follows = follows;
        _stories = stories;
        _presence = presence;
    }

    public async Task<DiscoveryPageResponse> DiscoverAsync(Guid userId, int page, int pageSize,
        string? city, bool premiumOnly, CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        if (page < 1 || pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
        var viewer = await _profiles.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("Kullanıcı profili bulunamadı.");
        var seen = await _profiles.GetRecentlySeenOrRejectedUserIdsAsync(userId,
            DateTime.UtcNow.AddHours(-24), cancellationToken);
        var excluded = (await _blocks.GetRelatedUserIdsAsync(userId, cancellationToken))
            .Concat(seen).Append(userId).Distinct().ToArray();
        var age = CalculateAge(viewer.BirthDate);
        List<UserProfile> profiles;
        bool hasMore;
        if (premiumOnly)
        {
            var rows = await _profiles.DiscoverAsync(userId, viewer.Gender, viewer.GenderPreference,
                age, viewer.MinimumMatchAge, viewer.MaximumMatchAge, city, excluded, true,
                (page - 1) * pageSize, pageSize + 1, cancellationToken);
            hasMore = rows.Count > pageSize;
            if (hasMore) rows.RemoveAt(rows.Count - 1);
            profiles = rows;
        }
        else
        {
            var premiumSlots = Math.Max(1, pageSize / 4);
            var regularSlots = Math.Max(0, pageSize - premiumSlots);
            var premiumRows = await _profiles.DiscoverAsync(userId, viewer.Gender, viewer.GenderPreference,
                age, viewer.MinimumMatchAge, viewer.MaximumMatchAge, city, excluded, true,
                (page - 1) * premiumSlots, premiumSlots + 1, cancellationToken);
            var regularRows = regularSlots == 0 ? [] : await _profiles.DiscoverAsync(userId,
                viewer.Gender, viewer.GenderPreference, age, viewer.MinimumMatchAge,
                viewer.MaximumMatchAge, city, excluded, false, (page - 1) * regularSlots,
                regularSlots + 1, cancellationToken);
            var premiumMore = premiumRows.Count > premiumSlots;
            var regularMore = regularRows.Count > regularSlots;
            if (premiumMore) premiumRows.RemoveAt(premiumRows.Count - 1);
            if (regularMore) regularRows.RemoveAt(regularRows.Count - 1);
            profiles = premiumRows.Concat(regularRows).ToList();
            hasMore = premiumMore || regularMore;
        }

        var viewerInterestIds = (await _interests.GetInterestIdsByUserIdAsync(userId, cancellationToken)).ToHashSet();
        var candidateIds = profiles.Select(x => x.UserId).ToArray();
        var interestRows = await _interests.GetByUserIdsAsync(candidateIds, cancellationToken);
        var interestsByUser = interestRows.GroupBy(x => x.UserId)
            .ToDictionary(x => x.Key, x => x.Select(y => y.InterestId).ToList());
        var musicRows = await _music.GetByUserIdsAsync(candidateIds.Append(userId).ToArray(), cancellationToken);
        var musicByUser = musicRows.GroupBy(x => x.UserId).ToDictionary(x => x.Key, x => x.ToList());
        var moodRows = await _moods.GetLatestActiveByUserIdsAsync(candidateIds.Append(userId).ToArray(),
            DateTime.UtcNow, cancellationToken);
        var moodByUser = moodRows.ToDictionary(x => x.UserId);
        var viewerMood = moodByUser.GetValueOrDefault(userId);

        var items = new List<UserProfileResponse>(profiles.Count);
        foreach (var profile in profiles)
        {
            var item = Map(profile);
            item.Age = CalculateAge(profile.BirthDate);
            ProfilePrivacyMapper.ApplyPublicView(item);
            item.ShowPremiumBadge = profile.ShowPremiumBadge && await _premium.IsPremiumAsync(profile.UserId, cancellationToken);
            item.InterestIds = interestsByUser.GetValueOrDefault(profile.UserId) ?? [];
            var commonInterests = viewerInterestIds.Intersect(item.InterestIds).Count();
            if (commonInterests > 0) item.DiscoveryReasons.Add($"{commonInterests} ortak ilgi alanı");
            if (musicByUser.TryGetValue(userId, out var ownMusic) && musicByUser.TryGetValue(profile.UserId, out var theirMusic))
            {
                var shared = ownMusic.Select(x => x.NormalizedGenre).Intersect(theirMusic.Select(x => x.NormalizedGenre))
                    .ToHashSet(StringComparer.Ordinal);
                foreach (var genre in theirMusic.Where(x => shared.Contains(x.NormalizedGenre))
                    .Select(x => x.Genre).Distinct(StringComparer.OrdinalIgnoreCase))
                    item.DiscoveryReasons.Add($"Ortak müzik tercihi: {genre}");
            }
            if (viewerMood is not null && moodByUser.TryGetValue(profile.UserId, out var theirMood) &&
                viewerMood.Mood == theirMood.Mood && viewerMood.DesiredConversation == theirMood.DesiredConversation)
                item.DiscoveryReasons.Add("Ruh hâli ve sohbet tercihi benzer");
            items.Add(item);
        }
        return new DiscoveryPageResponse { Items = items, Page = page, PageSize = pageSize, HasMore = hasMore };
    }

    public async Task RecordDecisionAsync(Guid userId, Guid targetUserId, bool rejected,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        ValidateUserId(targetUserId);
        if (userId == targetUserId) throw new ArgumentException("Kendi profiliniz için karar kaydedemezsiniz.");
        if (await _blocks.IsBlockedEitherWayAsync(userId, targetUserId, cancellationToken))
            throw new KeyNotFoundException("Profil bulunamadı.");
        var target = await _profiles.GetByUserIdAsync(targetUserId, cancellationToken);
        if (target is null || !target.IsProfileVisible) throw new KeyNotFoundException("Profil bulunamadı.");
        await _profiles.RecordDiscoveryDecisionAsync(userId, targetUserId, rejected, cancellationToken);
    }

    public async Task<DiscoverPeoplePageResponse> ListPeopleAsync(Guid userId, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        if (page < 1 || pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
        var selfSlot = page == 1 ? 1 : 0;
        var candidateCapacity = pageSize - selfSlot;
        var candidateOffset = page == 1 ? 0 : (pageSize - 1) + (page - 2) * pageSize;
        var blocked = await _blocks.GetRelatedUserIdsAsync(userId, cancellationToken);
        var rows = await _profiles.ListVisiblePeopleAsync(blocked.Append(userId).Distinct().ToArray(),
            candidateOffset, Math.Max(1, candidateCapacity + 1), cancellationToken);
        var hasMore = rows.Count > candidateCapacity;
        if (hasMore && candidateCapacity > 0) rows.RemoveAt(rows.Count - 1);
        if (candidateCapacity == 0) rows.Clear();
        var me = await _profiles.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("Kullanıcı profili bulunamadı.");
        var people = new List<UserProfile>();
        if (page == 1) people.Add(me);
        people.AddRange(rows);
        var ids = people.Select(x => x.UserId).ToArray();
        var storyRows = await _stories.GetFeedAsync(userId, blocked, 500, cancellationToken);
        storyRows.AddRange(await _stories.GetByUserIdAsync(userId, cancellationToken));
        storyRows = storyRows.Where(x => ids.Contains(x.UserId)).ToList();
        var storyOwners = storyRows.Select(x => x.UserId).ToHashSet();
        var seenStories = (await _stories.GetViewedStoryIdsAsync(userId, storyRows.Select(x => x.Id).ToArray(), cancellationToken)).ToHashSet();
        var unseenOwners = storyRows.Where(x => !seenStories.Contains(x.Id)).Select(x => x.UserId).ToHashSet();
        var statuses = await _presence.GetStatusesAsync(ids, cancellationToken);
        var items = new List<DiscoverPersonResponse>(people.Count);
        foreach (var profile in people)
        {
            var isSelf = profile.UserId == userId;
            var relation = isSelf ? FollowRelation.None : await GetRelationAsync(userId, profile.UserId, cancellationToken);
            var status = statuses.TryGetValue(profile.UserId, out var presence)
                ? presence : (IsOnline: false, LastSeenAtUtc: (DateTime?)null);
            var visibility = profile.ShowOnlineStatus && statuses.ContainsKey(profile.UserId);
            items.Add(new DiscoverPersonResponse
            {
                UserId = profile.UserId, DisplayName = profile.DisplayName, Nickname = profile.Nickname,
                IsSelf = isSelf, HasActiveStory = storyOwners.Contains(profile.UserId),
                HasUnseenStory = unseenOwners.Contains(profile.UserId),
                IsOnline = visibility ? status.IsOnline : null,
                LastSeenAtUtc = profile.ShowLastSeen && visibility ? status.LastSeenAtUtc : null,
                MessagingMode = isSelf ? DiscoverMessagingMode.Unavailable :
                    SocialAccess.ResolveMessaging(profile, true, false),
                FollowRelation = relation
            });
        }
        items = items.OrderByDescending(x => x.IsSelf).ThenByDescending(x => x.HasActiveStory)
            .ThenByDescending(x => x.IsOnline == true).ToList();
        return new DiscoverPeoplePageResponse { Items = items, Page = page, PageSize = pageSize, HasMore = hasMore };
    }

    private async Task<FollowRelation> GetRelationAsync(Guid viewerId, Guid targetId, CancellationToken cancellationToken)
    {
        var pair = await _follows.GetPairAsync(viewerId, targetId, cancellationToken);
        return UserFollow.RelationOf(pair.Outgoing, pair.Incoming);
    }

    private static UserProfileResponse Map(UserProfile profile) => new()
    {
        UserId = profile.UserId, DisplayName = profile.DisplayName, Nickname = profile.Nickname,
        Bio = profile.Bio, BirthDate = profile.BirthDate, Gender = profile.Gender,
        GenderPreference = profile.GenderPreference, MinimumMatchAge = profile.MinimumMatchAge,
        MaximumMatchAge = profile.MaximumMatchAge, City = profile.City,
        IsProfileVisible = profile.IsProfileVisible, AllowMessageRequests = profile.AllowMessageRequests,
        AllowCallsFromNonMatches = profile.AllowCallsFromNonMatches, ShowOnlineStatus = profile.ShowOnlineStatus,
        ShowLastSeen = profile.ShowLastSeen, ShowReadReceipts = profile.ShowReadReceipts,
        ShowPremiumBadge = profile.ShowPremiumBadge, PremiumPriorityOptIn = profile.PremiumPriorityOptIn
    };

    private static int? CalculateAge(DateOnly? birthDate)
    {
        if (!birthDate.HasValue) return null;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - birthDate.Value.Year;
        if (birthDate.Value > today.AddYears(-age)) age--;
        return age;
    }

    private static void ValidateUserId(Guid userId)
    {
        if (userId == Guid.Empty) throw new ArgumentException("Kullanıcı kimliği geçersiz.", nameof(userId));
    }
}
