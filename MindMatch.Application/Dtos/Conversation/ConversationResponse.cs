
namespace MindMatch.Application.Dtos.Conversation;

public class ConversationResponse
{
    public Guid Id { get; set; }

    public Guid OtherUserId { get; set; }

    public DateTime? LastMessageAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
