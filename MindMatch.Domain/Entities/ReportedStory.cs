using MindMatch.Domain.Common;

namespace MindMatch.Domain.Entities;

/// <summary>A time-limited moderation snapshot of a story reported by a user.</summary>
public sealed class ReportedStory : BaseEntity
{
    public const int DefaultEvidenceRetentionDays = 180;
    public Guid ReportId { get; private set; }
    public Guid? StoryId { get; private set; }
    public Guid? MediaAssetIdSnapshot { get; private set; }
    public string? MediaReferenceSnapshot { get; private set; }
    public StoryMediaType MediaType { get; private set; }
    public string? CaptionSnapshot { get; private set; }
    public DateTime StoryCreatedAtUtc { get; private set; }
    public DateTime EvidenceExpiresAtUtc { get; private set; }

    private ReportedStory() { }

    public ReportedStory(Guid reportId, Story story)
    {
        if (reportId == Guid.Empty || story is null) throw new ArgumentException("Rapor ve hikâye gereklidir.");
        ReportId = reportId;
        StoryId = story.Id;
        MediaAssetIdSnapshot = story.MediaAssetId;
        MediaReferenceSnapshot = story.MediaUrl;
        MediaType = story.MediaType;
        CaptionSnapshot = story.Caption;
        StoryCreatedAtUtc = story.CreatedAtUtc;
        EvidenceExpiresAtUtc = DateTime.UtcNow.AddDays(DefaultEvidenceRetentionDays);
    }

    public bool IsEvidenceExpiredAt(DateTime nowUtc) => EvidenceExpiresAtUtc <= DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);

    public bool IsMediaEvidenceAvailableAt(Guid mediaAssetId, DateTime nowUtc) =>
        mediaAssetId != Guid.Empty && MediaAssetIdSnapshot == mediaAssetId && !IsEvidenceExpiredAt(nowUtc);

    public bool ClearExpiredEvidence(DateTime nowUtc)
    {
        if (!IsEvidenceExpiredAt(nowUtc) ||
            (StoryId is null && MediaAssetIdSnapshot is null && MediaReferenceSnapshot is null && CaptionSnapshot is null))
            return false;
        StoryId = null;
        MediaAssetIdSnapshot = null;
        MediaReferenceSnapshot = null;
        CaptionSnapshot = null;
        MarkAsUpdated();
        return true;
    }
}
