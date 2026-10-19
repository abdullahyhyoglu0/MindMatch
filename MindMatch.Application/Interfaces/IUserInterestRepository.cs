using MindMatch.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MindMatch.Application.Interfaces
{



    public interface IUserInterestRepository
    {
        Task<List<Guid>> GetInterestIdsByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<List<UserInterest>> GetByUserIdsAsync(IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken = default);

        Task<bool> ReplaceForUserAsync(Guid userId, IReadOnlyCollection<Guid> interestIds,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsAsync(
            Guid userId,
            Guid interestId,
            CancellationToken cancellationToken = default);

        Task AddAsync(
            UserInterest userInterest,
            CancellationToken cancellationToken = default);

        Task RemoveAsync(
            Guid userId,
            Guid interestId,
            CancellationToken cancellationToken = default);
    }
}
