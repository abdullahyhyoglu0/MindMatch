using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;
using MindMatch.Infrastructure.Persistence;

namespace MindMatch.Infrastructure.Repositories;

public sealed class PremiumEntitlementRepository : IPremiumEntitlementRepository
{
    private readonly MindMatchDbContext _context;
    public PremiumEntitlementRepository(MindMatchDbContext context) => _context = context;
    public Task<PremiumEntitlement?> GetActiveAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken = default) =>
        _context.PremiumEntitlements.Where(x => x.UserId == userId && !x.IsRevoked && x.StartsAtUtc <= nowUtc && x.ExpiresAtUtc > nowUtc)
            .OrderByDescending(x => x.ExpiresAtUtc).FirstOrDefaultAsync(cancellationToken);
    public Task<bool> HasActiveAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken = default) =>
        _context.PremiumEntitlements.AnyAsync(x => x.UserId == userId && !x.IsRevoked && x.StartsAtUtc <= nowUtc && x.ExpiresAtUtc > nowUtc, cancellationToken);
    public Task AddAsync(PremiumEntitlement entitlement, CancellationToken cancellationToken = default) =>
        _context.PremiumEntitlements.AddAsync(entitlement, cancellationToken).AsTask();
}
