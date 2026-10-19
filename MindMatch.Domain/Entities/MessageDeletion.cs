using MindMatch.Domain.Common;

namespace MindMatch.Domain.Entities;

public class MessageDeletion : BaseEntity
{
    public Guid MessageId { get; private set; }
    public Guid UserId { get; private set; }

    private MessageDeletion() { }

    public MessageDeletion(Guid messageId, Guid userId)
    {
        if (messageId == Guid.Empty || userId == Guid.Empty)
            throw new ArgumentException("Mesaj ve kullanıcı kimlikleri gereklidir.");

        MessageId = messageId;
        UserId = userId;
    }
}
