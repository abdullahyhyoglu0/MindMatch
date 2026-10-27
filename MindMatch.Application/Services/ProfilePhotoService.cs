using MindMatch.Application.Dtos.UserProfile;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Services;

public sealed class ProfilePhotoService : IProfilePhotoService
{
    private const int MaxPhotos = 6;
    private readonly IProfilePhotoRepository _photos;
    private readonly IMediaAssetRepository _assets;
    private readonly IUserProfileRepository _profiles;
    private readonly IUserRepository _users;
    private readonly IUserBlockRepository _blocks;
    private readonly IUserFollowRepository? _follows;
    private readonly IUnitOfWork _unitOfWork;

    public ProfilePhotoService(IProfilePhotoRepository photos, IMediaAssetRepository assets,
        IUserProfileRepository profiles, IUserRepository users, IUserBlockRepository blocks, IUnitOfWork unitOfWork,
        IUserFollowRepository follows)
    { _photos = photos; _assets = assets; _profiles = profiles; _users = users; _blocks = blocks; _unitOfWork = unitOfWork; _follows = follows; }

    public ProfilePhotoService(IProfilePhotoRepository photos, IMediaAssetRepository assets,
        IUserProfileRepository profiles, IUserRepository users, IUserBlockRepository blocks, IUnitOfWork unitOfWork)
    { _photos = photos; _assets = assets; _profiles = profiles; _users = users; _blocks = blocks; _unitOfWork = unitOfWork; }

    public async Task<List<ProfilePhotoResponse>> GetAsync(Guid viewerId, Guid profileUserId, CancellationToken cancellationToken = default)
    {
        ValidateIds(viewerId, profileUserId);
        if (viewerId != profileUserId)
        {
            var profile = await _profiles.GetByUserIdAsync(profileUserId, cancellationToken);
            var owner = await _users.GetByIdAsync(profileUserId, cancellationToken);
            if (profile is null || owner is null || !owner.IsActive ||
                !(profile.IsProfileVisible || (_follows is not null &&
                    await _follows.IsAcceptedAsync(viewerId, profileUserId, cancellationToken))) ||
                await _blocks.IsBlockedEitherWayAsync(viewerId, profileUserId, cancellationToken))
                throw new KeyNotFoundException("Profil bulunamadı.");
        }
        return (await _photos.GetByUserIdAsync(profileUserId, cancellationToken)).Select(Map).ToList();
    }

    public async Task<ProfilePhotoResponse> AddAsync(Guid userId, Guid mediaAssetId, CancellationToken cancellationToken = default)
    {
        ValidateIds(userId, mediaAssetId);
        var existing = await _photos.GetByUserIdAsync(userId, cancellationToken);
        if (existing.Count >= MaxPhotos) throw new InvalidOperationException($"En fazla {MaxPhotos} profil fotoğrafı eklenebilir.");
        var asset = await _assets.GetByIdAsync(mediaAssetId, cancellationToken);
        if (asset is null || asset.OwnerId != userId || asset.IsDeleted || asset.Category != MediaCategory.ProfilePhoto)
            throw new UnauthorizedAccessException("Bu medya profil fotoğrafı olarak kullanılamaz.");
        if (await _photos.GetByMediaAssetIdAsync(mediaAssetId, cancellationToken) is not null)
            throw new InvalidOperationException("Bu medya zaten profil fotoğrafı olarak eklenmiş.");

        var photo = new ProfilePhoto(userId, mediaAssetId,
            existing.Count == 0 ? 0 : existing.Max(x => x.SortOrder) + 1, existing.Count == 0);
        await _photos.AddAsync(photo, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(photo);
    }

    public async Task SetPrimaryAsync(Guid userId, Guid photoId, CancellationToken cancellationToken = default)
    {
        ValidateIds(userId, photoId);
        var photo = await _photos.GetByIdAsync(photoId, cancellationToken)
            ?? throw new KeyNotFoundException("Profil fotoğrafı bulunamadı.");
        if (photo.UserId != userId) throw new UnauthorizedAccessException("Bu fotoğraf size ait değil.");
        if (await _photos.SetPrimaryForUserAsync(userId, photo.Id, cancellationToken) == 0)
            throw new KeyNotFoundException("Profil fotoğrafı bulunamadı.");
    }

    public async Task DeleteAsync(Guid userId, Guid photoId, CancellationToken cancellationToken = default)
    {
        ValidateIds(userId, photoId);
        var photo = await _photos.GetByIdAsync(photoId, cancellationToken)
            ?? throw new KeyNotFoundException("Profil fotoğrafı bulunamadı.");
        if (photo.UserId != userId) throw new UnauthorizedAccessException("Bu fotoğraf size ait değil.");
        var all = await _photos.GetByUserIdAsync(userId, cancellationToken);
        var asset = await _assets.GetByIdAsync(photo.MediaAssetId, cancellationToken);
        if (asset is not null) asset.MarkDeleted();
        _photos.Remove(photo);
        if (photo.IsPrimary)
        {
            var next = all.Where(x => x.Id != photo.Id).OrderBy(x => x.SortOrder).FirstOrDefault();
            await _photos.SetPrimaryForUserAsync(userId, next?.Id, cancellationToken);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static ProfilePhotoResponse Map(ProfilePhoto x) => new()
    { Id = x.Id, MediaAssetId = x.MediaAssetId, SortOrder = x.SortOrder, IsPrimary = x.IsPrimary };

    private static void ValidateIds(Guid a, Guid b)
    { if (a == Guid.Empty || b == Guid.Empty) throw new ArgumentException("Kimlik geçersiz."); }
}
