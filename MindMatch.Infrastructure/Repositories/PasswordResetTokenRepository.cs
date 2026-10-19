using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;
using MindMatch.Infrastructure.Persistence;

namespace MindMatch.Infrastructure.Repositories;

public sealed class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly MindMatchDbContext _context;
    public PasswordResetTokenRepository(MindMatchDbContext context) => _context = context;
    public Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken = default) =>
        _context.PasswordResetTokens.AddAsync(token, cancellationToken).AsTask();
    public Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        _context.PasswordResetTokens.SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
    public async Task RevokeActiveForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var tokens = await _context.PasswordResetTokens.Where(x =>
            x.UserId == userId && x.UsedAtUtc == null && x.ExpiresAtUtc > now).ToListAsync(cancellationToken);
        foreach (var token in tokens) token.Invalidate(now);
    }

    public async Task<int> DeleteInactiveBatchAsync(DateTime nowUtc, int take,
        CancellationToken cancellationToken = default)
    {
        if (take is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(take));
        nowUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        var tokens = await _context.PasswordResetTokens.Where(x =>
                x.UsedAtUtc != null || x.ExpiresAtUtc <= nowUtc)
            .OrderBy(x => x.ExpiresAtUtc).Take(take).ToListAsync(cancellationToken);
        _context.PasswordResetTokens.RemoveRange(tokens);
        return tokens.Count;
    }
}
