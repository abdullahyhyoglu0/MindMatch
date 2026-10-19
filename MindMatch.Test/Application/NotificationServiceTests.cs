using MindMatch.Application.Interfaces;
using MindMatch.Application.Services;
using MindMatch.Domain.Entities;

namespace MindMatch.Test.Application;

public sealed class NotificationServiceTests
{
    [Fact]
    public async Task QueueAsync_AddsNotificationAndRealtimeEventWithoutSavingTransaction()
    {
        var notifications = new FakeNotificationRepository();
        var outbox = new FakeOutboxRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new NotificationService(notifications, outbox, unitOfWork);

        await service.QueueAsync(Guid.NewGuid(), NotificationType.NewMessage,
            "Yeni mesaj", "Yeni bir mesajınız var.", Guid.NewGuid());

        Assert.Single(notifications.Added);
        Assert.Single(outbox.Added);
        Assert.Equal("NotificationReceived", outbox.Added[0].EventName);
        Assert.Equal(notifications.Added[0].UserId, outbox.Added[0].DestinationId);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_CommitsQueuedNotificationAndEventTogether()
    {
        var notifications = new FakeNotificationRepository();
        var outbox = new FakeOutboxRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new NotificationService(notifications, outbox, unitOfWork);

        await service.CreateAsync(Guid.NewGuid(), NotificationType.MatchAccepted,
            "Eşleşme kabul edildi", "Eşleşme isteğiniz kabul edildi.", Guid.NewGuid());

        Assert.Single(notifications.Added);
        Assert.Single(outbox.Added);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    private sealed class FakeNotificationRepository : INotificationRepository
    {
        public List<Notification> Added { get; } = [];
        public Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
        { Added.Add(notification); return Task.CompletedTask; }
        public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Notification?>(null);
        public Task<List<Notification>> GetByUserAsync(Guid userId, int take, DateTime? beforeCreatedAtUtc, Guid? beforeId, CancellationToken cancellationToken = default) => Task.FromResult(new List<Notification>());
        public Task<List<Notification>> GetUnreadByUserAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(new List<Notification>());
    }

    private sealed class FakeOutboxRepository : IRealtimeOutboxRepository
    {
        public List<RealtimeOutboxMessage> Added { get; } = [];
        public Task AddAsync(RealtimeOutboxMessage message, CancellationToken cancellationToken = default)
        { Added.Add(message); return Task.CompletedTask; }
        public Task<List<RealtimeOutboxMessage>> GetPendingBatchAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default) => Task.FromResult(new List<RealtimeOutboxMessage>());
        public Task DeleteExpiredBatchAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        { SaveCount++; return Task.FromResult(1); }
    }
}
