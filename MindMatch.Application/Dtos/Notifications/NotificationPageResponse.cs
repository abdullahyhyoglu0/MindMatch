namespace MindMatch.Application.Dtos.Notifications;

public sealed class NotificationPageResponse
{
    public IReadOnlyList<NotificationResponse> Items { get; init; } = [];
    public bool HasMore { get; init; }
    public DateTime? NextBeforeCreatedAtUtc { get; init; }
    public Guid? NextBeforeId { get; init; }
}
