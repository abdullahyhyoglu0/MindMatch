namespace MindMatch.Application.Dtos.Stories;

public sealed class StoryTrayItemResponse
{
    public Guid UserId { get; init; }
    public bool IsSelf { get; init; }
    public bool HasActiveStory { get; init; }
    public bool HasUnseenStory { get; init; }
    public bool? IsOnline { get; init; }
    public DateTime? LastSeenAtUtc { get; init; }
    public IReadOnlyList<StoryResponse> Stories { get; init; } = [];
}
