using MindMatch.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MindMatch.Application.Interfaces
{

    public interface IUserProfileRepository
    {
        Task<UserProfile?> GetByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task AddAsync(
            UserProfile profile,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsAsync(
            Guid userId,
            CancellationToken cancellationToken = default);
        Task<bool> IsNicknameTakenAsync(string normalizedNickname, Guid? exceptUserId = null,
            CancellationToken cancellationToken = default);

        Task<List<UserProfile>> DiscoverAsync(
            Guid viewerId, Gender viewerGender, GenderPreference targetGenderPreference,
            int? viewerAge, int? minimumAge, int? maximumAge, string? city,
            IReadOnlyCollection<Guid> excludedUserIds, bool premiumOnly,
            int skip, int take, CancellationToken cancellationToken = default);

        Task<List<Guid>> GetRecentlySeenOrRejectedUserIdsAsync(Guid viewerId, DateTime seenSinceUtc,
            CancellationToken cancellationToken = default);
        Task RecordDiscoveryDecisionAsync(Guid viewerId, Guid targetUserId, bool rejected,
            CancellationToken cancellationToken = default);
    }

}
