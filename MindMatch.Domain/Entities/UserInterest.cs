
using MindMatch.Domain.Common;

namespace MindMatch.Domain.Entities;

public class UserInterest : BaseEntity
{
    public Guid UserId { get; private set; }

    public Guid InterestId { get; private set; }

    private UserInterest()
    {
        // Entity Framework Core için.
    }

    public UserInterest(Guid userId, Guid interestId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("Kullanıcı kimliği geçersiz.");

        if (interestId == Guid.Empty)
            throw new ArgumentException("İlgi alanı kimliği geçersiz.");

        UserId = userId;
        InterestId = interestId;
    }
}
