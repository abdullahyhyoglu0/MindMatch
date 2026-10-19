using MindMatch.Domain.Common;

namespace MindMatch.Domain.Entities;

public sealed class ReportedMessage : BaseEntity
{
    public const int DefaultEvidenceRetentionDays = 180;
    public Guid ReportId { get; private set; }
    public Guid? MessageId { get; private set; }
    public Guid? MediaAssetIdSnapshot { get; private set; }
    public Guid SenderId { get; private set; }
    public string? ContentSnapshot { get; private set; }
    public string? MediaReferenceSnapshot { get; private set; }
    public MessageType MessageType { get; private set; }
    public DateTime MessageCreatedAtUtc { get; private set; }
    public DateTime EvidenceExpiresAtUtc { get; private set; }

    private ReportedMessage() { }

    public ReportedMessage(Guid reportId, Message message)
    {
        if (reportId == Guid.Empty) throw new ArgumentException("Rapor kimliği gereklidir.");
        ReportId = reportId;
        MessageId = message.Id;
        MediaAssetIdSnapshot = message.MediaAssetId;
        SenderId = message.SenderId;
        ContentSnapshot = message.Content;
        MediaReferenceSnapshot = message.MediaUrl;
        MessageType = message.Type;
        MessageCreatedAtUtc = message.CreatedAtUtc;
        EvidenceExpiresAtUtc = DateTime.UtcNow.AddDays(DefaultEvidenceRetentionDays);
    }

    public bool IsEvidenceExpiredAt(DateTime nowUtc) => EvidenceExpiresAtUtc <= DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);

    public bool IsMediaEvidenceAvailableAt(Guid mediaAssetId, DateTime nowUtc) =>
        mediaAssetId != Guid.Empty && MediaAssetIdSnapshot == mediaAssetId && !IsEvidenceExpiredAt(nowUtc);

    public bool ClearExpiredEvidence(DateTime nowUtc)
    {
        if (!IsEvidenceExpiredAt(nowUtc) ||
            (MessageId is null && MediaAssetIdSnapshot is null && ContentSnapshot is null && MediaReferenceSnapshot is null))
            return false;
        MessageId = null;
        MediaAssetIdSnapshot = null;
        ContentSnapshot = null;
        MediaReferenceSnapshot = null;
        MarkAsUpdated();
        return true;
    }
}
