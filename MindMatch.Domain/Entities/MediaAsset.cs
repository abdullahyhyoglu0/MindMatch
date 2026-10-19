using MindMatch.Domain.Common;

namespace MindMatch.Domain.Entities;

public enum MediaCategory { ProfilePhoto = 1, MessageImage = 2, MessageVoice = 3, StoryImage = 4, StoryVideo = 5, MessageVideo = 6 }

public sealed class MediaAsset : BaseEntity
{
    public Guid OwnerId { get; private set; }
    public MediaCategory Category { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long LengthBytes { get; private set; }
    public int? Width { get; private set; }
    public int? Height { get; private set; }
    public bool IsDeleted { get; private set; }
    public Guid? ConversationId { get; private set; }
    public Guid? BindingVersion { get; private set; }

    private MediaAsset() { }

    public MediaAsset(Guid ownerId, MediaCategory category, string storageKey, string contentType,
        long lengthBytes, int? width = null, int? height = null)
    {
        if (ownerId == Guid.Empty || string.IsNullOrWhiteSpace(storageKey) || string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Medya metadata bilgileri gereklidir.");
        if (!Enum.IsDefined(category) || lengthBytes < 1)
            throw new ArgumentException("Medya metadata bilgileri geçersiz.");
        OwnerId = ownerId;
        Category = category;
        StorageKey = storageKey;
        ContentType = contentType;
        LengthBytes = lengthBytes;
        Width = width;
        Height = height;
    }

    public void MarkDeleted()
    {
        BindingVersion = Guid.NewGuid();
        IsDeleted = true;
        MarkAsUpdated();
    }

    public void BindToConversation(Guid conversationId)
    {
        if (conversationId == Guid.Empty) throw new ArgumentException("Konuşma kimliği geçersiz.");
        if (ConversationId.HasValue && ConversationId.Value != conversationId)
            throw new InvalidOperationException("Medya başka bir konuşmaya bağlı.");
        if (ConversationId == conversationId) return;
        ConversationId = conversationId;
        BindingVersion = Guid.NewGuid();
        MarkAsUpdated();
    }
}
