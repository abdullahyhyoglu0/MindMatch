
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Interfaces;

public interface IMoodCheckInRepository
{
    Task<MoodCheckIn?> GetLatestByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<List<MoodCheckIn>> GetLatestActiveByUserIdsAsync(IReadOnlyCollection<Guid> userIds,
        DateTime nowUtc, CancellationToken cancellationToken = default);

    Task AddAsync(
        MoodCheckIn moodCheckIn,
        CancellationToken cancellationToken = default);
}
