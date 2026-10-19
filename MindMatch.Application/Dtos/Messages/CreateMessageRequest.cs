using System.ComponentModel.DataAnnotations;

namespace MindMatch.Application.Dtos.Message;

public sealed class CreateMessageRequest
{
    [Required] public Guid RecipientId { get; set; }
    [Required, MaxLength(5000)] public string Content { get; set; } = string.Empty;
}

public sealed class MessageRequestResponse
{
    public Guid ConversationId { get; init; }
    public Guid SenderId { get; init; }
    public Guid ReceiverId { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public string? InitialMessage { get; init; }
}
