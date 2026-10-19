using MindMatch.Domain.Common;

namespace MindMatch.Domain.Entities;

public sealed class UserBlock : BaseEntity
{
    public Guid BlockerId { get; private set; }
    public Guid BlockedUserId { get; private set; }

    private UserBlock() { }

    public UserBlock(Guid blockerId, Guid blockedUserId)
    {
        if (blockerId == Guid.Empty || blockedUserId == Guid.Empty)
            throw new ArgumentException("Kullanıcı kimlikleri gereklidir.");
        if (blockerId == blockedUserId)
            throw new ArgumentException("Kullanıcı kendisini engelleyemez.");

        BlockerId = blockerId;
        BlockedUserId = blockedUserId;
    }
}
