using MindMatch.Domain.Entities;

namespace MindMatch.Application.Interfaces;

public interface IPremiumEntitlementRepository
{
    Task<PremiumEntitlement?> GetActiveAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken = default);
    Task<bool> HasActiveAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken = default);
    Task AddAsync(PremiumEntitlement entitlement, CancellationToken cancellationToken = default);
}
