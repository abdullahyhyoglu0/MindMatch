
using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;
using MindMatch.Infrastructure.Persistence;

namespace MindMatch.Infrastructure.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly MindMatchDbContext _context;

    public RefreshTokenRepository(MindMatchDbContext context)
    {
        _context = context;
    }

    public async Task<RefreshToken?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        return await _context.RefreshTokens.FirstOrDefaultAsync(
            token => token.TokenHash == tokenHash,
            cancellationToken);
    }

    public async Task AddAsync(
        RefreshToken refreshToken,
        CancellationToken cancellationToken = default)
    {
        await _context.RefreshTokens.AddAsync(
            refreshToken,
            cancellationToken);
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var tokens = await _context.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null && x.ExpiresAtUtc > DateTime.UtcNow)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens) token.Revoke();
    }

    public async Task<int> DeleteInactiveBatchAsync(DateTime nowUtc, int take,
        CancellationToken cancellationToken = default)
    {
        if (take is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(take));
        nowUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        var tokens = await _context.RefreshTokens.Where(x =>
                x.RevokedAtUtc != null || x.ExpiresAtUtc <= nowUtc)
            .OrderBy(x => x.ExpiresAtUtc).Take(take).ToListAsync(cancellationToken);
        _context.RefreshTokens.RemoveRange(tokens);
        return tokens.Count;
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
