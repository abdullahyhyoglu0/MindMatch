namespace MindMatch.Application.Dtos.UserProfile;

public sealed class ProfilePhotoResponse
{
    public Guid Id { get; init; }
    public Guid MediaAssetId { get; init; }
    public int SortOrder { get; init; }
    public bool IsPrimary { get; init; }
}
