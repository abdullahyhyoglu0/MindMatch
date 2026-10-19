using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;
using MindMatch.Infrastructure.Persistence;

namespace MindMatch.Infrastructure.Repositories;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly MindMatchDbContext _context;
    public NotificationRepository(MindMatchDbContext context) => _context = context;
    public Task AddAsync(Notification notification, CancellationToken cancellationToken = default) =>
        _context.Notifications.AddAsync(notification, cancellationToken).AsTask();
    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Notifications.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<List<Notification>> GetByUserAsync(Guid userId, int take, DateTime? beforeCreatedAtUtc, Guid? beforeId, CancellationToken cancellationToken = default)
    {
        var query = _context.Notifications.Where(x => x.UserId == userId);
        if (beforeCreatedAtUtc.HasValue && beforeId.HasValue)
            query = query.Where(x => x.CreatedAtUtc < beforeCreatedAtUtc.Value || (x.CreatedAtUtc == beforeCreatedAtUtc.Value && x.Id.CompareTo(beforeId.Value) < 0));
        return query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).Take(take).ToListAsync(cancellationToken);
    }
    public Task<List<Notification>> GetUnreadByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.Notifications.Where(x => x.UserId == userId && x.ReadAtUtc == null).ToListAsync(cancellationToken);
}
