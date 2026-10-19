namespace MindMatch.Application.Dtos.UserProfile;

public sealed class DiscoveryPageResponse
{
    public IReadOnlyList<UserProfileResponse> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public bool HasMore { get; init; }
}
