using MindMatch.Application.Dtos.Message;

namespace MindMatch.Application.Interfaces;

public interface IMessageService
{
    Task<MessageResponse> SendAsync(
        Guid senderId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<MessagePageResponse> GetHistoryPageAsync(
        Guid conversationId,
        Guid userId,
        DateTime? beforeCreatedAtUtc,
        Guid? beforeMessageId,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task DeleteForMeAsync(
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<MessageResponse> DeleteForEveryoneAsync(
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task MarkDeliveredAsync(Guid messageId, Guid userId, CancellationToken cancellationToken = default);

    Task<MessageResponse> MarkReadAsync(Guid messageId, Guid userId, CancellationToken cancellationToken = default);
}
