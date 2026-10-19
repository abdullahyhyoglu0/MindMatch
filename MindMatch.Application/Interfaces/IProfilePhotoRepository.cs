using MindMatch.Domain.Entities;

namespace MindMatch.Application.Interfaces;

public interface IProfilePhotoRepository
{
    Task<List<ProfilePhoto>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ProfilePhoto?> GetByIdAsync(Guid photoId, CancellationToken cancellationToken = default);
    Task<ProfilePhoto?> GetByMediaAssetIdAsync(Guid mediaAssetId, CancellationToken cancellationToken = default);
    Task AddAsync(ProfilePhoto photo, CancellationToken cancellationToken = default);
    Task<int> SetPrimaryForUserAsync(Guid userId, Guid? primaryPhotoId, CancellationToken cancellationToken = default);
    void Remove(ProfilePhoto photo);
}
