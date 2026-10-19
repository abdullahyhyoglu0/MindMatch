using MindMatch.Domain.Common;

namespace MindMatch.Domain.Entities;

public enum ModerationActionType { ReportAssigned = 1, ReportResolved = 2, UserFrozen = 3, UserActivated = 4, ReportViewed = 5, UserFreezeExpired = 6, ReportEvidenceViewed = 7 }

public sealed class ModerationAudit : BaseEntity
{
    public Guid ActorId { get; private set; }
    public Guid? TargetUserId { get; private set; }
    public Guid? ReportId { get; private set; }
    public ModerationActionType Action { get; private set; }
    public string Reason { get; private set; } = string.Empty;

    private ModerationAudit() { }

    public ModerationAudit(Guid actorId, ModerationActionType action, string reason, Guid? targetUserId = null, Guid? reportId = null)
    {
        if (actorId == Guid.Empty || string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("İşlemi yapan ve nedeni gereklidir.");
        if (!Enum.IsDefined(action) || reason.Length > 1000)
            throw new ArgumentException("Moderasyon kaydı geçersiz.");
        ActorId = actorId;
        Action = action;
        Reason = reason.Trim();
        TargetUserId = targetUserId;
        ReportId = reportId;
    }
}
