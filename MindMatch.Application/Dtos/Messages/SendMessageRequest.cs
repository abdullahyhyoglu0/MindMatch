using MindMatch.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace MindMatch.Application.Dtos.Message;

public class SendMessageRequest
{
    [Required]
    public Guid ConversationId { get; set; }

    [Required]
    public MessageType Type { get; set; }

    [MaxLength(5000)]
    public string? Content { get; set; }

    [MaxLength(2048)]
    public string? MediaUrl { get; set; }
    public Guid? MediaAssetId { get; set; }
}
