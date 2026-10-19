
using MindMatch.Domain.Common;

namespace MindMatch.Domain.Entities;

public class Message : BaseEntity
{
    public Guid ConversationId { get; private set; }

    public Guid SenderId { get; private set; }

    public string? Content { get; private set; }

    public MessageType Type { get; private set; }

    public MessageStatus Status { get; private set; }

    public string? MediaUrl { get; private set; }
    public Guid? MediaAssetId { get; private set; }

    public DateTime? ReadAtUtc { get; private set; }
    public bool IsDeletedForEveryone { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    public DateTime? ExpiresAtUtc { get; private set; }

    public ICollection<MessageDeletion> Deletions { get; private set; }
        = new List<MessageDeletion>();

    public Conversation Conversation { get; private set; } = null!;

    private Message()
    {
    }

    public Message(
        Guid conversationId,
        Guid senderId,
        MessageType type,
        string? content = null,
        string? mediaUrl = null,
        Guid? mediaAssetId = null)
    {
        if (conversationId == Guid.Empty)
            throw new ArgumentException("Sohbet ID boş olamaz.");

        if (senderId == Guid.Empty)
            throw new ArgumentException("Gönderen ID boş olamaz.");

        if (string.IsNullOrWhiteSpace(content) &&
            string.IsNullOrWhiteSpace(mediaUrl) && !mediaAssetId.HasValue)
        {
            throw new ArgumentException(
                "Mesaj metin veya medya içermelidir.");
        }

        ConversationId = conversationId;
        SenderId = senderId;
        Type = type;
        Content = content;
        MediaUrl = mediaUrl;
        MediaAssetId = mediaAssetId;
        Status = MessageStatus.Sent;
    }

    public void MarkAsDelivered()
    {
        if (Status == MessageStatus.Read)
            return;

        Status = MessageStatus.Delivered;
        MarkAsUpdated();
    }

    public void MarkAsRead()
    {
        if (Status == MessageStatus.Read)
            return;

        Status = MessageStatus.Read;
        ReadAtUtc = DateTime.UtcNow;
        MarkAsUpdated();
    }

    public void DeleteForEveryone(Guid requestingUserId)
    {
        if (requestingUserId != SenderId)
            throw new UnauthorizedAccessException("Mesajı herkesten yalnızca gönderen silebilir.");
        if (IsDeletedForEveryone)
            return;

        IsDeletedForEveryone = true;
        DeletedAtUtc = DateTime.UtcNow;
        Content = null;
        MediaUrl = null;
        MediaAssetId = null;
        MarkAsUpdated();
    }

    public void SetExpiration(DateTime expiresAtUtc)
    {
        if (expiresAtUtc <= CreatedAtUtc)
            throw new ArgumentOutOfRangeException(nameof(expiresAtUtc));

        ExpiresAtUtc = DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc);
        MarkAsUpdated();
    }

    public bool ClearExpiredContent(DateTime nowUtc)
    {
        if (!ExpiresAtUtc.HasValue || ExpiresAtUtc.Value > nowUtc || IsDeletedForEveryone)
            return false;

        IsDeletedForEveryone = true;
        DeletedAtUtc = nowUtc;
        Content = null;
        MediaUrl = null;
        MediaAssetId = null;
        MarkAsUpdated();
        return true;
    }
}

public enum MessageType
{
    Text = 1,
    Image = 2,
    Voice = 3,
    Video = 4,
    File = 5
}

public enum MessageStatus
{
    Sent = 1,
    Delivered = 2,
    Read = 3
}
