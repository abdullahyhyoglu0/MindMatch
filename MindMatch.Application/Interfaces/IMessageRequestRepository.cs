using MindMatch.Domain.Entities;

namespace MindMatch.Application.Interfaces;

public interface IMessageRequestRepository
{
    Task<MessageRequest?> GetByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<List<MessageRequest>> GetIncomingAsync(Guid receiverId, CancellationToken cancellationToken = default);
    Task AddAsync(MessageRequest request, CancellationToken cancellationToken = default);
}
