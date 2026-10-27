using MindMatch.Domain.Common;

namespace MindMatch.Domain.Entities;

public enum NotificationType
{
    MatchRequestReceived = 1,
    MatchAccepted = 2,
    MessageRequestReceived = 3,
    MessageRequestAccepted = 4,
    NewMessage = 5,
    IncomingCall = 6,
    ReportResolved = 7,
    FollowRequestReceived = 8,
    FollowAccepted = 9,
    NewFollower = 10
}

public sealed class Notification : BaseEntity
{
    public Guid UserId { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public Guid? ReferenceId { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }

    private Notification() { }

    public Notification(Guid userId, NotificationType type, string title, string body, Guid? referenceId = null)
    {
        if (userId == Guid.Empty || !Enum.IsDefined(type)) throw new ArgumentException("Bildirim bilgileri geçersiz.");
        if (string.IsNullOrWhiteSpace(title) || title.Length > 120 || string.IsNullOrWhiteSpace(body) || body.Length > 500)
            throw new ArgumentException("Bildirim başlığı veya metni geçersiz.");
        UserId = userId;
        Type = type;
        Title = title.Trim();
        Body = body.Trim();
        ReferenceId = referenceId;
    }

    public void MarkRead(Guid userId)
    {
        if (userId != UserId) throw new UnauthorizedAccessException("Bu bildirim size ait değil.");
        if (ReadAtUtc.HasValue) return;
        ReadAtUtc = DateTime.UtcNow;
        MarkAsUpdated();
    }
}
