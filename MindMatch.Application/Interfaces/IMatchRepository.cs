
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Interfaces;

public interface IMatchRepository
{
    Task<Match?> GetByIdAsync(
        Guid matchId,
        CancellationToken cancellationToken = default);

    Task<bool> HasPendingOrAcceptedMatchAsync(
        Guid userId,
        Guid otherUserId,
        CancellationToken cancellationToken = default);

    Task<bool> HasAcceptedMatchAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken = default);

    Task<List<Match>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<List<Match>> GetIncomingPendingAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AddAsync(
        Match match,
        CancellationToken cancellationToken = default);

    void Update(Match match);
}
