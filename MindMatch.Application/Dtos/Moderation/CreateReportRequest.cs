using System.ComponentModel.DataAnnotations;
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Dtos.Moderation;

public sealed class CreateReportRequest
{
    [Required] public Guid ReportedUserId { get; set; }
    [Required] public ReportReason Reason { get; set; }
    [MaxLength(1000)] public string? Description { get; set; }
    public Guid? ConversationId { get; set; }
    public List<Guid> MessageIds { get; set; } = [];
    public Guid? StoryId { get; set; }
}
