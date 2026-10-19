
using MindMatch.Domain.Common;

namespace MindMatch.Domain.Entities;

public class Story : BaseEntity
{
    public Guid UserId { get; private set; }

    public StoryMediaType MediaType { get; private set; }

    public string MediaUrl { get; private set; } = string.Empty;
    public Guid? MediaAssetId { get; private set; }

    public string? Caption { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public bool IsDeleted { get; private set; }

    public ICollection<StoryView> Views { get; private set; }
        = new List<StoryView>();

    private Story() { }

    public Story(
        Guid userId,
        StoryMediaType mediaType,
        string mediaUrl,
        string? caption = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("Kullanıcı ID boş olamaz.");

        if (string.IsNullOrWhiteSpace(mediaUrl))
            throw new ArgumentException("Medya adresi boş olamaz.");

        UserId = userId;
        MediaType = mediaType;
        MediaUrl = mediaUrl;
        Caption = caption;
        ExpiresAtUtc = DateTime.UtcNow.AddHours(24);
    }

    public Story(Guid userId, StoryMediaType mediaType, Guid mediaAssetId, string? caption = null)
    {
        if (userId == Guid.Empty || mediaAssetId == Guid.Empty)
            throw new ArgumentException("Hikâye kullanıcısı ve medya kimliği gereklidir.");
        if (!Enum.IsDefined(mediaType)) throw new ArgumentException("Hikâye medya türü geçersiz.");
        if (caption?.Length > 500) throw new ArgumentException("Hikâye yazısı en fazla 500 karakter olabilir.");
        UserId = userId;
        MediaType = mediaType;
        MediaAssetId = mediaAssetId;
        Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
        ExpiresAtUtc = DateTime.UtcNow.AddHours(24);
    }

    public bool IsExpired()
    {
        return DateTime.UtcNow >= ExpiresAtUtc;
    }

    public bool IsAvailableAt(DateTime nowUtc) => !IsDeleted && ExpiresAtUtc > DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);

    public void Delete()
    {
        IsDeleted = true;
        MediaAssetId = null;
        MediaUrl = string.Empty;
        MarkAsUpdated();
    }
}

public enum StoryMediaType
{
    Image = 1,
    Video = 2
}
