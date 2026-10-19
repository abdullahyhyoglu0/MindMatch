
using MindMatch.Application.Dtos.Conversation;

namespace MindMatch.Application.Interfaces;

public interface IConversationService
{
    Task<List<ConversationResponse>> GetMyConversationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task HideConversationAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ConversationResponse> GetAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default);
    Task RestoreConversationAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default);
}
