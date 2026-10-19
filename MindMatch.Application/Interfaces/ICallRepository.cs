using MindMatch.Domain.Entities;

namespace MindMatch.Application.Interfaces;

public interface ICallRepository
{
    Task<Call?> GetByIdAsync(Guid callId, CancellationToken cancellationToken = default);
    Task AddAsync(Call call, CancellationToken cancellationToken = default);
    Task<List<Call>> GetMissedBatchAsync(DateTime cutoffUtc, int take, CancellationToken cancellationToken = default);
    Task<List<Call>> GetByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default);
}
