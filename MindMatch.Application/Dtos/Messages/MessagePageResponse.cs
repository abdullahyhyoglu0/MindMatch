namespace MindMatch.Application.Dtos.Message;

public sealed class MessagePageResponse
{
    public IReadOnlyList<MessageResponse> Items { get; init; } = [];
    public bool HasMore { get; init; }
    public DateTime? NextBeforeCreatedAtUtc { get; init; }
    public Guid? NextBeforeMessageId { get; init; }
}
