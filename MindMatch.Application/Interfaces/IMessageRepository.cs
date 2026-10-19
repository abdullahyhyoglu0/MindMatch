using MindMatch.Domain.Entities;

namespace MindMatch.Application.Interfaces;

public interface IMessageRepository
{
    Task<Message?> GetByIdAsync(
        Guid messageId,
        CancellationToken cancellationToken = default);

    Task<Message?> GetFirstBySenderAsync(
        Guid conversationId,
        Guid senderId,
        CancellationToken cancellationToken = default);

    Task<List<Message>> GetVisiblePageAsync(
        Guid conversationId,
        Guid userId,
        DateTime? beforeCreatedAtUtc,
        Guid? beforeMessageId,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task AddDeletionAsync(
        MessageDeletion deletion,
        CancellationToken cancellationToken = default);

    Task<bool> HasDeletionAsync(
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<List<Message>> GetByIdsAsync(
        IReadOnlyCollection<Guid> messageIds,
        CancellationToken cancellationToken = default);

    Task<Message?> GetByMediaAssetIdAsync(Guid mediaAssetId, CancellationToken cancellationToken = default);
    Task<List<Message>> GetExpiredBatchAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default);

    Task AddAsync(
        Message message,
        CancellationToken cancellationToken = default);
}
