using MindMatch.Application.Dtos.Calls;
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Interfaces;

public interface ICallService
{
    Task<CallResponse> StartAsync(Guid callerId, Guid conversationId, CallType type, CancellationToken cancellationToken = default);
    Task<CallResponse> AnswerAsync(Guid callId, Guid userId, CancellationToken cancellationToken = default);
    Task<CallResponse> RejectAsync(Guid callId, Guid userId, CancellationToken cancellationToken = default);
    Task<CallResponse> EndAsync(Guid callId, Guid userId, CancellationToken cancellationToken = default);
    Task<CallResponse> GetStatusAsync(Guid callId, Guid userId, CancellationToken cancellationToken = default);
    Task<CallResponse> GetForSignalingAsync(Guid callId, Guid userId, CancellationToken cancellationToken = default);
    Task<List<CallResponse>> GetByConversationAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default);
}
