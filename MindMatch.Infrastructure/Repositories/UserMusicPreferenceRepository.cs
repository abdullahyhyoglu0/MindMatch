using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;
using MindMatch.Infrastructure.Persistence;

namespace MindMatch.Infrastructure.Repositories;

public sealed class UserMusicPreferenceRepository : IUserMusicPreferenceRepository
{
    private readonly MindMatchDbContext _context;
    public UserMusicPreferenceRepository(MindMatchDbContext context) => _context = context;

    public Task<List<UserMusicPreference>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.UserMusicPreferences.AsNoTracking().Where(x => x.UserId == userId)
            .OrderBy(x => x.NormalizedGenre).ToListAsync(cancellationToken);

    public Task<List<UserMusicPreference>> GetByUserIdsAsync(IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default) =>
        _context.UserMusicPreferences.AsNoTracking().Where(x => userIds.Contains(x.UserId))
            .ToListAsync(cancellationToken);

    public async Task ReplaceAsync(Guid userId, IReadOnlyCollection<string> genres, CancellationToken cancellationToken = default)
    {
        var current = await _context.UserMusicPreferences.Where(x => x.UserId == userId).ToListAsync(cancellationToken);
        var desired = genres.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var desiredKeys = desired.Select(x => x.ToUpperInvariant()).ToHashSet(StringComparer.Ordinal);
        _context.UserMusicPreferences.RemoveRange(current.Where(x => !desiredKeys.Contains(x.NormalizedGenre)));
        var existingKeys = current.Select(x => x.NormalizedGenre).ToHashSet(StringComparer.Ordinal);
        foreach (var genre in desired.Where(x => !existingKeys.Contains(x.ToUpperInvariant())))
            await _context.UserMusicPreferences.AddAsync(new UserMusicPreference(userId, genre), cancellationToken);
    }
}
