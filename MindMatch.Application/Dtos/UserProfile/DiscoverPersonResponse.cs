using MindMatch.Domain.Entities;

namespace MindMatch.Application.Dtos.UserProfile;

public sealed class DiscoverPersonResponse
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Nickname { get; init; } = string.Empty;
    public bool IsSelf { get; init; }
    public bool HasActiveStory { get; init; }
    public bool HasUnseenStory { get; init; }
    public bool? IsOnline { get; init; }
    public DateTime? LastSeenAtUtc { get; init; }
    public DiscoverMessagingMode MessagingMode { get; init; }
    public FollowRelation FollowRelation { get; init; }
}

public sealed class DiscoverPeoplePageResponse
{
    public IReadOnlyList<DiscoverPersonResponse> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public bool HasMore { get; init; }
}
