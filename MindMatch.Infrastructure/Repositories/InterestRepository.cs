
using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;
using MindMatch.Infrastructure.Persistence;

namespace MindMatch.Infrastructure.Repositories;

public class InterestRepository : IInterestRepository
{
    private readonly MindMatchDbContext _context;

    public InterestRepository(MindMatchDbContext context)
    {
        _context = context;
    }

    public Task<List<Interest>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return _context.Interests
            .OrderBy(interest => interest.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<Interest?> GetByIdAsync(
        Guid interestId,
        CancellationToken cancellationToken = default)
    {
        return _context.Interests
            .SingleOrDefaultAsync(
                interest => interest.Id == interestId,
                cancellationToken);
    }

    public Task<bool> ExistsAsync(
        Guid interestId,
        CancellationToken cancellationToken = default)
    {
        return _context.Interests
            .AnyAsync(
                interest => interest.Id == interestId,
                cancellationToken);
    }
}
