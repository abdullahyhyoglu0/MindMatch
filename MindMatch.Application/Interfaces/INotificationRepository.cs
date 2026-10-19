using MindMatch.Domain.Entities;

namespace MindMatch.Application.Interfaces;

public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Notification>> GetByUserAsync(Guid userId, int take, DateTime? beforeCreatedAtUtc, Guid? beforeId, CancellationToken cancellationToken = default);
    Task<List<Notification>> GetUnreadByUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
