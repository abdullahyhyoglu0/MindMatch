using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;
using MindMatch.Infrastructure.Persistence;

namespace MindMatch.Infrastructure.Repositories;

public sealed class ProfilePhotoRepository : IProfilePhotoRepository
{
    private readonly MindMatchDbContext _context;
    public ProfilePhotoRepository(MindMatchDbContext context) => _context = context;
    public Task<List<ProfilePhoto>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.ProfilePhotos.Where(x => x.UserId == userId).OrderBy(x => x.SortOrder).ToListAsync(cancellationToken);
    public Task<ProfilePhoto?> GetByIdAsync(Guid photoId, CancellationToken cancellationToken = default) =>
        _context.ProfilePhotos.SingleOrDefaultAsync(x => x.Id == photoId, cancellationToken);
    public Task<ProfilePhoto?> GetByMediaAssetIdAsync(Guid mediaAssetId, CancellationToken cancellationToken = default) =>
        _context.ProfilePhotos.SingleOrDefaultAsync(x => x.MediaAssetId == mediaAssetId, cancellationToken);
    public Task AddAsync(ProfilePhoto photo, CancellationToken cancellationToken = default) =>
        _context.ProfilePhotos.AddAsync(photo, cancellationToken).AsTask();
    public async Task<int> SetPrimaryForUserAsync(Guid userId, Guid? primaryPhotoId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var affected = await _context.ProfilePhotos.Where(x => x.UserId == userId)
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.IsPrimary, false)
                .SetProperty(x => x.UpdatedAtUtc, now), cancellationToken);
        if (primaryPhotoId.HasValue)
        {
            var selected = await _context.ProfilePhotos.Where(x => x.UserId == userId && x.Id == primaryPhotoId.Value)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(x => x.IsPrimary, true)
                    .SetProperty(x => x.UpdatedAtUtc, now), cancellationToken);
            if (selected == 0) return 0;
        }
        await transaction.CommitAsync(cancellationToken);
        return affected;
    }
    public void Remove(ProfilePhoto photo) => _context.ProfilePhotos.Remove(photo);
}
