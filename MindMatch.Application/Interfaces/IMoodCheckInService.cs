
using MindMatch.Application.Dtos.MoodCheckIn;

namespace MindMatch.Application.Interfaces;

public interface IMoodCheckInService
{
    Task<MoodCheckInResponse> CreateAsync(
        Guid userId,
        CreateMoodCheckInRequest request,
        CancellationToken cancellationToken = default);

    Task<MoodCheckInResponse?> GetMyLatestAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
