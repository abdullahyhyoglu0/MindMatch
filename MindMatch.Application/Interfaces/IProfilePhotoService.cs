using MindMatch.Application.Dtos.UserProfile;

namespace MindMatch.Application.Interfaces;

public interface IProfilePhotoService
{
    Task<List<ProfilePhotoResponse>> GetAsync(Guid viewerId, Guid profileUserId, CancellationToken cancellationToken = default);
    Task<ProfilePhotoResponse> AddAsync(Guid userId, Guid mediaAssetId, CancellationToken cancellationToken = default);
    Task SetPrimaryAsync(Guid userId, Guid photoId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid userId, Guid photoId, CancellationToken cancellationToken = default);
}
