
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Interfaces;

public interface IInterestRepository
{
    Task<List<Interest>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<Interest?> GetByIdAsync(
        Guid interestId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        Guid interestId,
        CancellationToken cancellationToken = default);
}
