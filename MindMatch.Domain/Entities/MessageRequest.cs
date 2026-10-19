using MindMatch.Domain.Common;

namespace MindMatch.Domain.Entities;

public enum MessageRequestStatus { Pending = 1, Accepted = 2, Rejected = 3 }

public sealed class MessageRequest : BaseEntity
{
    public Guid ConversationId { get; private set; }
    public Guid SenderId { get; private set; }
    public Guid ReceiverId { get; private set; }
    public MessageRequestStatus Status { get; private set; } = MessageRequestStatus.Pending;
    public int Version { get; private set; } = 1;
    public DateTime? RespondedAtUtc { get; private set; }

    private MessageRequest() { }

    public MessageRequest(Guid conversationId, Guid senderId, Guid receiverId)
    {
        if (conversationId == Guid.Empty || senderId == Guid.Empty || receiverId == Guid.Empty || senderId == receiverId)
            throw new ArgumentException("Mesaj isteği için kimlikler geçerli olmalıdır.");
        ConversationId = conversationId;
        SenderId = senderId;
        ReceiverId = receiverId;
    }

    public void Accept(Guid userId)
    {
        EnsureReceiver(userId);
        EnsurePending();
        Status = MessageRequestStatus.Accepted;
        RespondedAtUtc = DateTime.UtcNow;
        Version++;
        MarkAsUpdated();
    }

    public void Reject(Guid userId)
    {
        EnsureReceiver(userId);
        EnsurePending();
        Status = MessageRequestStatus.Rejected;
        RespondedAtUtc = DateTime.UtcNow;
        Version++;
        MarkAsUpdated();
    }

    public void MarkMatched()
    {
        if (Status != MessageRequestStatus.Accepted)
        {
            Status = MessageRequestStatus.Accepted;
            RespondedAtUtc = DateTime.UtcNow;
            Version++;
            MarkAsUpdated();
        }
    }

    private void EnsureReceiver(Guid userId)
    {
        if (userId != ReceiverId) throw new UnauthorizedAccessException("Mesaj isteğini yalnızca alıcı yanıtlayabilir.");
    }

    private void EnsurePending()
    {
        if (Status != MessageRequestStatus.Pending) throw new InvalidOperationException("Mesaj isteği artık yanıtlanamaz.");
    }
}
