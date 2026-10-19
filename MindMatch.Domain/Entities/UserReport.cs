using MindMatch.Domain.Common;

namespace MindMatch.Domain.Entities;

public enum ReportReason { Harassment = 1, Insult = 2, SexualContent = 3, Spam = 4, FakeProfile = 5, Threat = 6, InappropriateMedia = 7, Other = 8 }
public enum ReportStatus { Pending = 1, UnderReview = 2, Resolved = 3, Dismissed = 4 }

public sealed class UserReport : BaseEntity
{
    public Guid ReporterId { get; private set; }
    public Guid ReportedUserId { get; private set; }
    public Guid? ConversationId { get; private set; }
    public ReportReason Reason { get; private set; }
    public string? Description { get; private set; }
    public ReportStatus Status { get; private set; } = ReportStatus.Pending;
    public Guid? AssignedModeratorId { get; private set; }
    public string? Resolution { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }
    public ICollection<ReportedMessage> Messages { get; private set; } = new List<ReportedMessage>();

    private UserReport() { }

    public UserReport(Guid reporterId, Guid reportedUserId, ReportReason reason, string? description, Guid? conversationId)
    {
        if (reporterId == Guid.Empty || reportedUserId == Guid.Empty || reporterId == reportedUserId)
            throw new ArgumentException("Raporlayan ve raporlanan kullanıcılar geçerli olmalıdır.");
        if (!Enum.IsDefined(reason)) throw new ArgumentException("Rapor nedeni geçersiz.");
        if (description?.Length > 1000) throw new ArgumentException("Açıklama en fazla 1000 karakter olabilir.");
        if (conversationId == Guid.Empty) throw new ArgumentException("Sohbet kimliği geçersiz.");
        ReporterId = reporterId;
        ReportedUserId = reportedUserId;
        Reason = reason;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        ConversationId = conversationId;
    }

    public void AddMessageSnapshot(ReportedMessage message)
    {
        if (message.ReportId != Id) throw new InvalidOperationException("Mesaj kanıtı farklı bir rapora ait.");
        Messages.Add(message);
    }

    public void Assign(Guid moderatorId)
    {
        if (moderatorId == Guid.Empty) throw new ArgumentException("Moderatör kimliği geçersiz.");
        if (Status is ReportStatus.Resolved or ReportStatus.Dismissed)
            throw new InvalidOperationException("Tamamlanmış rapor atanamaz.");
        AssignedModeratorId = moderatorId;
        Status = ReportStatus.UnderReview;
        MarkAsUpdated();
    }

    public void Resolve(Guid moderatorId, string resolution, bool violationConfirmed)
    {
        if (AssignedModeratorId != moderatorId)
            throw new UnauthorizedAccessException("Yalnızca atanmış moderatör raporu sonuçlandırabilir.");
        if (string.IsNullOrWhiteSpace(resolution) || resolution.Length > 1000)
            throw new ArgumentException("Sonuç açıklaması 1-1000 karakter olmalıdır.");
        Status = violationConfirmed ? ReportStatus.Resolved : ReportStatus.Dismissed;
        Resolution = resolution.Trim();
        ResolvedAtUtc = DateTime.UtcNow;
        MarkAsUpdated();
    }
}
