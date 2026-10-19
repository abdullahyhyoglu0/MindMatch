using MindMatch.Domain.Common;

namespace MindMatch.Domain.Entities;

public sealed class ProfilePhoto : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid MediaAssetId { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsPrimary { get; private set; }

    private ProfilePhoto() { }

    public ProfilePhoto(Guid userId, Guid mediaAssetId, int sortOrder, bool isPrimary = false)
    {
        if (userId == Guid.Empty || mediaAssetId == Guid.Empty || sortOrder < 0)
            throw new ArgumentException("Profil fotoğrafı bilgileri geçersiz.");
        UserId = userId;
        MediaAssetId = mediaAssetId;
        SortOrder = sortOrder;
        IsPrimary = isPrimary;
    }

    public void SetPrimary(bool isPrimary)
    {
        IsPrimary = isPrimary;
        MarkAsUpdated();
    }

    public void SetSortOrder(int sortOrder)
    {
        if (sortOrder < 0) throw new ArgumentOutOfRangeException(nameof(sortOrder));
        SortOrder = sortOrder;
        MarkAsUpdated();
    }
}
