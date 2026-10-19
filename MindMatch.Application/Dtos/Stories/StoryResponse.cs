using MindMatch.Domain.Entities;

namespace MindMatch.Application.Dtos.Stories;

public sealed class StoryResponse
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public Guid? MediaAssetId { get; init; }
    public StoryMediaType MediaType { get; init; }
    public string? Caption { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
    public bool HasViewed { get; init; }
}
