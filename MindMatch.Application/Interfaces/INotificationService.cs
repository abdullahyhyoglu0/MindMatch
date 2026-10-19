using MindMatch.Application.Dtos.Notifications;
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Interfaces;

public interface INotificationService
{
    Task CreateAsync(Guid userId, NotificationType type, string title, string body, Guid? referenceId, CancellationToken cancellationToken = default);
    Task QueueAsync(Guid userId, NotificationType type, string title, string body, Guid? referenceId, CancellationToken cancellationToken = default);
    Task<NotificationPageResponse> GetMineAsync(Guid userId, int pageSize, DateTime? beforeCreatedAtUtc, Guid? beforeId, CancellationToken cancellationToken = default);
    Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default);
    Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default);
}
