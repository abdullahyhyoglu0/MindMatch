
using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;
using MindMatch.Infrastructure.Persistence;

namespace MindMatch.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly MindMatchDbContext _context;

    public UserRepository(MindMatchDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        return await _context.Users.FirstOrDefaultAsync(
            user => user.Email.ToLower() == normalizedEmail,
            cancellationToken);
    }

    public async Task<User?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Users.FirstOrDefaultAsync(
            user => user.Id == userId,
            cancellationToken);
    }

    public async Task AddAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        await _context.Users.AddAsync(user, cancellationToken);
    }

    public async Task<bool> EmailExistsAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        return await _context.Users.AnyAsync(
            user => user.Email.ToLower() == normalizedEmail,
            cancellationToken);
    }

    public Task<List<User>> GetExpiredAdminFrozenBatchAsync(DateTime nowUtc, int take,
        CancellationToken cancellationToken = default) =>
        _context.Users.Where(x => x.IsFrozenByAdmin && x.AdminFrozenUntilUtc.HasValue &&
                x.AdminFrozenUntilUtc.Value <= nowUtc)
            .OrderBy(x => x.AdminFrozenUntilUtc).Take(take).ToListAsync(cancellationToken);

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
