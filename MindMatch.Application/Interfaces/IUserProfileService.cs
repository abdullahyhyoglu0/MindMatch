
using MindMatch.Application.Dtos.UserProfile;


namespace MindMatch.Application.Interfaces;

public interface IUserProfileService
{
    Task<UserProfileResponse> GetMyProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<UserProfileResponse> UpdateMyProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default);

    Task SetProfileVisibilityAsync(
        Guid userId,
        bool isVisible,
        CancellationToken cancellationToken = default);

    Task<UserProfileResponse> GetPublicProfileAsync(Guid viewerId, Guid targetUserId, CancellationToken cancellationToken = default);
    Task UpdatePreferencesAsync(Guid userId, ProfilePreferencesRequest request, CancellationToken cancellationToken = default);
    Task UpdateInterestsAsync(Guid userId, IReadOnlyCollection<Guid> interestIds, CancellationToken cancellationToken = default);
}
