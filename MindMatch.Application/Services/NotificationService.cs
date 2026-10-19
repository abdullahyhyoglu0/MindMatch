using MindMatch.Application.Dtos.Notifications;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Services;

public sealed class NotificationService : INotificationService
{
    private readonly INotificationRepository _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeOutboxRepository _outbox;
    public NotificationService(INotificationRepository notifications, IRealtimeOutboxRepository outbox, IUnitOfWork unitOfWork)
    { _notifications = notifications; _outbox = outbox; _unitOfWork = unitOfWork; }

    public async Task CreateAsync(Guid userId, NotificationType type, string title, string body, Guid? referenceId, CancellationToken cancellationToken = default)
    {
        await QueueAsync(userId, type, title, body, referenceId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task QueueAsync(Guid userId, NotificationType type, string title, string body, Guid? referenceId, CancellationToken cancellationToken = default)
    {
        var notification = new Notification(userId, type, title, body, referenceId);
        await _notifications.AddAsync(notification, cancellationToken);
        await _outbox.AddAsync(RealtimeEventOutbox.ToUser(userId, "NotificationReceived", new
        {
            notification.Id, type, title, body, referenceId, notification.CreatedAtUtc
        }, notification.CreatedAtUtc.AddDays(30)), cancellationToken);
    }

    public async Task<NotificationPageResponse> GetMineAsync(Guid userId, int pageSize, DateTime? beforeCreatedAtUtc, Guid? beforeId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || pageSize is < 1 or > 100 || beforeCreatedAtUtc.HasValue != beforeId.HasValue)
            throw new ArgumentException("Bildirim sayfalama bilgileri geçersiz.");
        var rows = await _notifications.GetByUserAsync(userId, pageSize + 1, beforeCreatedAtUtc, beforeId, cancellationToken);
        var hasMore = rows.Count > pageSize;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        return new NotificationPageResponse
        {
            Items = rows.Select(x => new NotificationResponse
            {
                Id = x.Id, Type = x.Type, Title = x.Title, Body = x.Body,
                ReferenceId = x.ReferenceId, CreatedAtUtc = x.CreatedAtUtc, ReadAtUtc = x.ReadAtUtc
            }).ToList(),
            HasMore = hasMore,
            NextBeforeCreatedAtUtc = hasMore ? rows[^1].CreatedAtUtc : null,
            NextBeforeId = hasMore ? rows[^1].Id : null
        };
    }

    public async Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _notifications.GetByIdAsync(notificationId, cancellationToken)
            ?? throw new KeyNotFoundException("Bildirim bulunamadı.");
        notification.MarkRead(userId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) throw new ArgumentException("Kullanıcı kimliği geçersiz.");
        var unread = await _notifications.GetUnreadByUserAsync(userId, cancellationToken);
        foreach (var notification in unread) notification.MarkRead(userId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
