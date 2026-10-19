using MindMatch.Application.Dtos.Message;

namespace MindMatch.Application.Interfaces;

public interface IMessageRequestService
{
    Task<MessageResponse> CreateAsync(Guid senderId, CreateMessageRequest request, CancellationToken cancellationToken = default);
    Task<List<MessageRequestResponse>> GetIncomingAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AcceptAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default);
    Task RejectAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default);
}
