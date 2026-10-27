using MindMatch.Domain.Entities;

namespace MindMatch.Application.Dtos.Moderation;

public sealed class ReportResponse
{
    public Guid Id { get; init; }
    public Guid ReporterId { get; init; }
    public Guid ReportedUserId { get; init; }
    public Guid? ConversationId { get; init; }
    public ReportReason Reason { get; init; }
    public string? Description { get; init; }
    public ReportStatus Status { get; init; }
    public Guid? AssignedModeratorId { get; init; }
    public string? Resolution { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? ResolvedAtUtc { get; init; }
    public List<ReportedMessageResponse> Messages { get; init; } = [];
    public List<ReportedStoryResponse> Stories { get; init; } = [];
}

public sealed class ReportedMessageResponse
{
    public Guid? MessageId { get; init; }
    public Guid? MediaAssetIdSnapshot { get; init; }
    public Guid SenderId { get; init; }
    public string? ContentSnapshot { get; init; }
    public string? MediaReferenceSnapshot { get; init; }
    public MessageType MessageType { get; init; }
    public DateTime MessageCreatedAtUtc { get; init; }
}

public sealed class ReportedStoryResponse
{
    public Guid? StoryId { get; init; }
    public Guid? MediaAssetIdSnapshot { get; init; }
    public string? MediaReferenceSnapshot { get; init; }
    public StoryMediaType MediaType { get; init; }
    public string? CaptionSnapshot { get; init; }
    public DateTime StoryCreatedAtUtc { get; init; }
    public DateTime EvidenceExpiresAtUtc { get; init; }
}
