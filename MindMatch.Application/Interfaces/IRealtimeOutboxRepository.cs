using MindMatch.Domain.Entities;

namespace MindMatch.Application.Interfaces;

public interface IRealtimeOutboxRepository
{
    Task AddAsync(RealtimeOutboxMessage message, CancellationToken cancellationToken = default);
    Task<List<RealtimeOutboxMessage>> GetPendingBatchAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default);
    Task DeleteExpiredBatchAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default);
}
