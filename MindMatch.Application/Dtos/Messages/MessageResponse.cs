using MindMatch.Domain.Entities;

namespace MindMatch.Application.Dtos.Message;

public class MessageResponse
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public Guid? RecipientId { get; set; }
    public string? Content { get; set; }
    public MessageType Type { get; set; }
    public MessageStatus Status { get; set; }
    public string? MediaUrl { get; set; }
    public Guid? MediaAssetId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public bool IsDeletedForEveryone { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public bool IsPendingRequest { get; set; }
}
