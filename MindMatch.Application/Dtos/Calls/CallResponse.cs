using MindMatch.Domain.Entities;

namespace MindMatch.Application.Dtos.Calls;

public sealed class CallResponse
{
    public Guid Id { get; init; }
    public Guid ConversationId { get; init; }
    public Guid CallerId { get; init; }
    public Guid ReceiverId { get; init; }
    public CallType Type { get; init; }
    public CallStatus Status { get; init; }
    public DateTime StartedAtUtc { get; init; }
    public DateTime? AnsweredAtUtc { get; init; }
    public DateTime? EndedAtUtc { get; init; }
}
