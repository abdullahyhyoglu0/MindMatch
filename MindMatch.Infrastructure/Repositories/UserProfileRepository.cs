
using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;
using MindMatch.Infrastructure.Persistence;

namespace MindMatch.Infrastructure.Repositories;

public class UserProfileRepository : IUserProfileRepository
{
    private readonly MindMatchDbContext _context;

    public UserProfileRepository(MindMatchDbContext context)
    {
        _context = context;
    }

    public Task<UserProfile?> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _context.UserProfiles
            .SingleOrDefaultAsync(
                profile => profile.UserId == userId,
                cancellationToken);
    }

    public async Task AddAsync(
        UserProfile profile,
        CancellationToken cancellationToken = default)
    {
        await _context.UserProfiles.AddAsync(profile, cancellationToken);
    }

    public Task<bool> ExistsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _context.UserProfiles
            .AnyAsync(
                profile => profile.UserId == userId,
                cancellationToken);
    }

    public Task<bool> IsNicknameTakenAsync(string normalizedNickname, Guid? exceptUserId = null,
        CancellationToken cancellationToken = default) =>
        _context.UserProfiles.AnyAsync(x => x.NormalizedNickname == normalizedNickname &&
            (!exceptUserId.HasValue || x.UserId != exceptUserId.Value), cancellationToken);

    public Task<List<UserProfile>> DiscoverAsync(
        Guid viewerId, Gender viewerGender, GenderPreference targetGenderPreference,
        int? viewerAge, int? minimumAge, int? maximumAge, string? city,
        IReadOnlyCollection<Guid> excludedUserIds, bool premiumOnly,
        int skip, int take, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = _context.UserProfiles.Where(p => p.UserId != viewerId && p.IsProfileVisible &&
            !excludedUserIds.Contains(p.UserId) &&
            _context.Users.Any(u => u.Id == p.UserId && u.IsActive));

        if (targetGenderPreference != GenderPreference.Everyone)
        {
            var requiredGender = targetGenderPreference switch
            {
                GenderPreference.Male => Gender.Male,
                GenderPreference.Female => Gender.Female,
                _ => Gender.NonBinary
            };
            query = query.Where(p => p.Gender == requiredGender);
        }

        if (viewerGender == Gender.PreferNotToSay)
            query = query.Where(p => p.GenderPreference == GenderPreference.Everyone);
        else
        {
            var viewerPreference = viewerGender switch
            {
                Gender.Male => GenderPreference.Male,
                Gender.Female => GenderPreference.Female,
                _ => GenderPreference.NonBinary
            };
            query = query.Where(p => p.GenderPreference == GenderPreference.Everyone || p.GenderPreference == viewerPreference);
        }

        if (viewerAge.HasValue)
            query = query.Where(p => (!p.MinimumMatchAge.HasValue || p.MinimumMatchAge <= viewerAge.Value) &&
                                     (!p.MaximumMatchAge.HasValue || p.MaximumMatchAge >= viewerAge.Value));
        if (minimumAge.HasValue || maximumAge.HasValue)
        {
            var min = minimumAge ?? 18;
            var max = maximumAge ?? 100;
            var earliest = today.AddYears(-(max + 1)).AddDays(1);
            var latest = today.AddYears(-min);
            query = query.Where(p => p.BirthDate.HasValue && p.BirthDate.Value >= earliest && p.BirthDate.Value <= latest);
        }
        if (!string.IsNullOrWhiteSpace(city))
        {
            var normalizedCity = city.Trim().ToLower();
            query = query.Where(p => p.City != null && p.City.ToLower() == normalizedCity);
        }

        var now = DateTime.UtcNow;
        var premium = query.Where(p => p.PremiumPriorityOptIn && _context.PremiumEntitlements.Any(e =>
            e.UserId == p.UserId && !e.IsRevoked && e.StartsAtUtc <= now && e.ExpiresAtUtc > now));
        if (premiumOnly)
            return OrderCandidates(premium, viewerId).Skip(skip).Take(take).ToListAsync(cancellationToken);

        var regular = query.Where(p => !p.PremiumPriorityOptIn || !_context.PremiumEntitlements.Any(e =>
            e.UserId == p.UserId && !e.IsRevoked && e.StartsAtUtc <= now && e.ExpiresAtUtc > now));
        return OrderCandidates(regular, viewerId).Skip(skip).Take(take).ToListAsync(cancellationToken);
    }

    private IOrderedQueryable<UserProfile> OrderCandidates(IQueryable<UserProfile> query, Guid viewerId) =>
        query.OrderByDescending(profile => _context.UserInterests.Count(candidateInterest =>
                candidateInterest.UserId == profile.UserId &&
                _context.UserInterests.Any(viewerInterest => viewerInterest.UserId == viewerId &&
                    viewerInterest.InterestId == candidateInterest.InterestId)))
            .ThenByDescending(profile => _context.UserMusicPreferences.Count(candidateMusic =>
                candidateMusic.UserId == profile.UserId &&
                _context.UserMusicPreferences.Any(viewerMusic => viewerMusic.UserId == viewerId &&
                    viewerMusic.NormalizedGenre == candidateMusic.NormalizedGenre)))
            .ThenByDescending(profile =>
                _context.MoodCheckIns.Any(viewerMood => viewerMood.UserId == viewerId && viewerMood.ExpiresAtUtc > DateTime.UtcNow) &&
                _context.MoodCheckIns.Any(candidateMood => candidateMood.UserId == profile.UserId &&
                    candidateMood.ExpiresAtUtc > DateTime.UtcNow) &&
                _context.MoodCheckIns.Where(viewerMood => viewerMood.UserId == viewerId && viewerMood.ExpiresAtUtc > DateTime.UtcNow)
                    .OrderByDescending(viewerMood => viewerMood.CreatedAtUtc).Select(viewerMood => viewerMood.Mood).FirstOrDefault() ==
                _context.MoodCheckIns.Where(candidateMood => candidateMood.UserId == profile.UserId && candidateMood.ExpiresAtUtc > DateTime.UtcNow)
                    .OrderByDescending(candidateMood => candidateMood.CreatedAtUtc).Select(candidateMood => candidateMood.Mood).FirstOrDefault() &&
                _context.MoodCheckIns.Where(viewerMood => viewerMood.UserId == viewerId && viewerMood.ExpiresAtUtc > DateTime.UtcNow)
                    .OrderByDescending(viewerMood => viewerMood.CreatedAtUtc).Select(viewerMood => viewerMood.DesiredConversation).FirstOrDefault() ==
                _context.MoodCheckIns.Where(candidateMood => candidateMood.UserId == profile.UserId && candidateMood.ExpiresAtUtc > DateTime.UtcNow)
                    .OrderByDescending(candidateMood => candidateMood.CreatedAtUtc).Select(candidateMood => candidateMood.DesiredConversation).FirstOrDefault())
            .ThenByDescending(profile => profile.CreatedAtUtc)
            .ThenBy(profile => profile.UserId);

    public Task<List<Guid>> GetRecentlySeenOrRejectedUserIdsAsync(Guid viewerId, DateTime seenSinceUtc,
        CancellationToken cancellationToken = default) =>
        _context.DiscoveryDecisions.AsNoTracking()
            .Where(x => x.ViewerId == viewerId && (x.RejectedAtUtc != null || x.SeenAtUtc >= seenSinceUtc))
            .Select(x => x.TargetUserId).ToListAsync(cancellationToken);

    public async Task RecordDiscoveryDecisionAsync(Guid viewerId, Guid targetUserId, bool rejected,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var rejectedAt = rejected ? now : (DateTime?)null;
        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO discovery_decisions ("Id", "ViewerId", "TargetUserId", "SeenAtUtc", "RejectedAtUtc", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ({Guid.NewGuid()}, {viewerId}, {targetUserId}, {now}, {rejectedAt}, {now}, {now})
            ON CONFLICT ("ViewerId", "TargetUserId") DO UPDATE SET
              "SeenAtUtc" = EXCLUDED."SeenAtUtc",
              "RejectedAtUtc" = COALESCE(discovery_decisions."RejectedAtUtc", EXCLUDED."RejectedAtUtc"),
              "UpdatedAtUtc" = EXCLUDED."UpdatedAtUtc"
            """, cancellationToken);
    }
}
