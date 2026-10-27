using MindMatch.Domain.Common;

namespace MindMatch.Domain.Entities;

public enum FollowStatus
{
    Pending = 1,
    Accepted = 2
}

public enum FollowRelation
{
    None = 0,
    Following = 1,
    FollowedBy = 2,
    Mutual = 3,
    OutgoingRequest = 4,
    IncomingRequest = 5
}

public enum DiscoverMessagingMode
{
    Unavailable = 0,
    Direct = 1,
    Request = 2
}

public sealed class UserFollow : BaseEntity
{
    public Guid FollowerId { get; private set; }
    public Guid FollowedId { get; private set; }
    public FollowStatus Status { get; private set; } = FollowStatus.Pending;
    public int Version { get; private set; } = 1;
    public DateTime? RespondedAtUtc { get; private set; }

    private UserFollow() { }

    public UserFollow(Guid followerId, Guid followedId, bool targetProfileIsVisible)
    {
        if (followerId == Guid.Empty || followedId == Guid.Empty)
            throw new ArgumentException("Kullanıcı kimlikleri gereklidir.");
        if (followerId == followedId)
            throw new ArgumentException("Kullanıcı kendisini takip edemez.");

        FollowerId = followerId;
        FollowedId = followedId;
        Status = targetProfileIsVisible ? FollowStatus.Accepted : FollowStatus.Pending;
        if (Status == FollowStatus.Accepted)
            RespondedAtUtc = DateTime.UtcNow;
    }

    public void Accept(Guid followedId)
    {
        if (followedId != FollowedId)
            throw new UnauthorizedAccessException("Takip isteğini yalnızca alıcı kabul edebilir.");
        EnsurePending();
        Status = FollowStatus.Accepted;
        RespondedAtUtc = DateTime.UtcNow;
        Version++;
        MarkAsUpdated();
    }

    public void EnsurePending()
    {
        if (Status != FollowStatus.Pending)
            throw new InvalidOperationException("Takip isteği artık yanıtlanamaz.");
    }

    public static FollowRelation RelationOf(UserFollow? outgoing, UserFollow? incoming)
    {
        var following = outgoing?.Status == FollowStatus.Accepted;
        var followedBy = incoming?.Status == FollowStatus.Accepted;
        if (following && followedBy) return FollowRelation.Mutual;
        if (following) return FollowRelation.Following;
        if (followedBy) return FollowRelation.FollowedBy;
        if (outgoing?.Status == FollowStatus.Pending) return FollowRelation.OutgoingRequest;
        if (incoming?.Status == FollowStatus.Pending) return FollowRelation.IncomingRequest;
        return FollowRelation.None;
    }
}
