using MindMatch.Domain.Entities;

namespace MindMatch.Application.Interfaces;

public interface IUserMusicPreferenceRepository
{
    Task<List<UserMusicPreference>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<UserMusicPreference>> GetByUserIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);
    Task ReplaceAsync(Guid userId, IReadOnlyCollection<string> genres, CancellationToken cancellationToken = default);
}
