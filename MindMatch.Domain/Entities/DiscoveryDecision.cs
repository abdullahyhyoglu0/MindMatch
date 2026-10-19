using MindMatch.Domain.Common;

namespace MindMatch.Domain.Entities;

public sealed class DiscoveryDecision : BaseEntity
{
    public Guid ViewerId { get; private set; }
    public Guid TargetUserId { get; private set; }
    public DateTime SeenAtUtc { get; private set; }
    public DateTime? RejectedAtUtc { get; private set; }

    private DiscoveryDecision() { }

    public DiscoveryDecision(Guid viewerId, Guid targetUserId)
    {
        if (viewerId == Guid.Empty || targetUserId == Guid.Empty || viewerId == targetUserId)
            throw new ArgumentException("Keşif kullanıcı kimlikleri geçersiz.");
        ViewerId = viewerId;
        TargetUserId = targetUserId;
        SeenAtUtc = DateTime.UtcNow;
    }

    public void MarkSeen()
    {
        SeenAtUtc = DateTime.UtcNow;
        MarkAsUpdated();
    }

    public void Reject()
    {
        RejectedAtUtc ??= DateTime.UtcNow;
        MarkAsUpdated();
    }
}
