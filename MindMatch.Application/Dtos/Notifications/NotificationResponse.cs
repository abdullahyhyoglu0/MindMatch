using MindMatch.Domain.Entities;

namespace MindMatch.Application.Dtos.Notifications;

public sealed class NotificationResponse
{
    public Guid Id { get; init; }
    public NotificationType Type { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public Guid? ReferenceId { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? ReadAtUtc { get; init; }
}
