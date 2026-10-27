using MindMatch.Domain.Entities;

namespace MindMatch.Application.Dtos.Follow;

public sealed class FollowResponse
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Nickname { get; init; } = string.Empty;
    public FollowStatus Status { get; init; }
    public FollowRelation Relation { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class FollowListResponse
{
    public IReadOnlyList<FollowResponse> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public bool HasMore { get; init; }
    public bool IsHidden { get; init; }
    public int Count { get; init; }
}
