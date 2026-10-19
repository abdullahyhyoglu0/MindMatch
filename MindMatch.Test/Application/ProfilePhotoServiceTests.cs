using MindMatch.Application.Interfaces;
using MindMatch.Application.Services;
using MindMatch.Domain.Entities;

namespace MindMatch.Test.Application;

public sealed class ProfilePhotoServiceTests
{
    [Fact]
    public async Task AddAndSetPrimary_MaintainsSinglePrimaryPhoto()
    {
        var fixture = new Fixture();
        var first = await fixture.Service.AddAsync(fixture.Owner.Id, fixture.AddAsset());
        var second = await fixture.Service.AddAsync(fixture.Owner.Id, fixture.AddAsset());

        Assert.True(first.IsPrimary);
        Assert.False(second.IsPrimary);
        await fixture.Service.SetPrimaryAsync(fixture.Owner.Id, second.Id);
        Assert.False(fixture.Photos.Single(x => x.Id == first.Id).IsPrimary);
        Assert.True(fixture.Photos.Single(x => x.Id == second.Id).IsPrimary);
    }

    [Fact]
    public async Task SetPrimary_RejectsAnotherUsersPhoto()
    {
        var fixture = new Fixture();
        var otherUser = new User("other@test.invalid", "hash");
        var photo = await fixture.Service.AddAsync(otherUser.Id, fixture.AddAsset(otherUser.Id));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fixture.Service.SetPrimaryAsync(fixture.Owner.Id, photo.Id));
    }

    [Fact]
    public async Task DeletePrimary_SelectsNextPhotoAndMarksOnlyItsAssetDeleted()
    {
        var fixture = new Fixture();
        var firstAssetId = fixture.AddAsset();
        var secondAssetId = fixture.AddAsset();
        var first = await fixture.Service.AddAsync(fixture.Owner.Id, firstAssetId);
        var second = await fixture.Service.AddAsync(fixture.Owner.Id, secondAssetId);

        await fixture.Service.DeleteAsync(fixture.Owner.Id, first.Id);

        Assert.DoesNotContain(fixture.Photos, x => x.Id == first.Id);
        Assert.True(fixture.Photos.Single(x => x.Id == second.Id).IsPrimary);
        Assert.True(fixture.GetAsset(firstAssetId).IsDeleted);
        Assert.False(fixture.GetAsset(secondAssetId).IsDeleted);
    }

    private sealed class Fixture
    {
        public User Owner { get; } = new("owner@test.invalid", "hash");
        public List<ProfilePhoto> Photos { get; } = [];
        private readonly Dictionary<Guid, MediaAsset> _assets = [];
        public ProfilePhotoService Service { get; }

        public Fixture() => Service = new ProfilePhotoService(new PhotoRepo(this), new AssetRepo(this),
            new Profiles(), new Users(this), new Blocks(), new UnitOfWork());

        public Guid AddAsset(Guid? ownerId = null)
        {
            var asset = new MediaAsset(ownerId ?? Owner.Id, MediaCategory.ProfilePhoto,
                $"profilephoto/{Guid.NewGuid():N}.png", "image/png", 128, 1, 1);
            _assets.Add(asset.Id, asset);
            return asset.Id;
        }

        public MediaAsset GetAsset(Guid id) => _assets[id];

        private sealed class PhotoRepo(Fixture f) : IProfilePhotoRepository
        {
            public Task<List<ProfilePhoto>> GetByUserIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(f.Photos.Where(x => x.UserId == id).OrderBy(x => x.SortOrder).ToList());
            public Task<ProfilePhoto?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(f.Photos.SingleOrDefault(x => x.Id == id));
            public Task<ProfilePhoto?> GetByMediaAssetIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(f.Photos.SingleOrDefault(x => x.MediaAssetId == id));
            public Task AddAsync(ProfilePhoto photo, CancellationToken ct = default) { f.Photos.Add(photo); return Task.CompletedTask; }
            public Task<int> SetPrimaryForUserAsync(Guid userId, Guid? primaryId, CancellationToken ct = default)
            {
                var photos = f.Photos.Where(x => x.UserId == userId).ToList();
                if (primaryId.HasValue && photos.All(x => x.Id != primaryId.Value)) return Task.FromResult(0);
                foreach (var item in photos) item.SetPrimary(item.Id == primaryId);
                return Task.FromResult(photos.Count);
            }
            public void Remove(ProfilePhoto photo) => f.Photos.Remove(photo);
        }

        private sealed class AssetRepo(Fixture f) : IMediaAssetRepository
        {
            public Task<MediaAsset?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(f._assets.GetValueOrDefault(id));
            public Task AddAsync(MediaAsset asset, CancellationToken ct = default) => Task.CompletedTask;
            public Task<bool> IsUsedInMessageAsync(Guid id, CancellationToken ct = default) => Task.FromResult(false);
            public Task<bool> IsReferencedAsync(Guid id, CancellationToken ct = default) => Task.FromResult(false);
            public Task<List<MediaAsset>> GetDeletedUnreferencedBatchAsync(int take, CancellationToken ct = default) => Task.FromResult(new List<MediaAsset>());
            public Task<List<MediaAsset>> GetExpiredUnattachedBatchAsync(DateTime before, int take, CancellationToken ct = default) => Task.FromResult(new List<MediaAsset>());
            public void Remove(MediaAsset asset) => f._assets.Remove(asset.Id);
        }

        private sealed class Profiles : IUserProfileRepository
        {
            public Task<UserProfile?> GetByUserIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<UserProfile?>(null);
            public Task AddAsync(UserProfile profile, CancellationToken ct = default) => Task.CompletedTask;
            public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) => Task.FromResult(false);
            public Task<bool> IsNicknameTakenAsync(string nickname, Guid? exceptUserId = null, CancellationToken ct = default) => Task.FromResult(false);
            public Task<List<UserProfile>> DiscoverAsync(Guid viewerId, Gender gender, GenderPreference preference, int? age, int? min, int? max, string? city, IReadOnlyCollection<Guid> excluded, bool premiumOnly, int skip, int take, CancellationToken ct = default) => Task.FromResult(new List<UserProfile>());
            public Task<List<Guid>> GetRecentlySeenOrRejectedUserIdsAsync(Guid id, DateTime since, CancellationToken ct = default) => Task.FromResult(new List<Guid>());
            public Task RecordDiscoveryDecisionAsync(Guid viewerId, Guid targetId, bool rejected, CancellationToken ct = default) => Task.CompletedTask;
        }

        private sealed class Users(Fixture f) : IUserRepository
        {
            public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) => Task.FromResult<User?>(null);
            public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(id == f.Owner.Id ? f.Owner : null);
            public Task AddAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
            public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default) => Task.FromResult(false);
            public Task<List<User>> GetExpiredAdminFrozenBatchAsync(DateTime nowUtc, int take, CancellationToken ct = default) => Task.FromResult(new List<User>());
            public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
        }

        private sealed class Blocks : IUserBlockRepository
        {
            public Task<bool> IsBlockedEitherWayAsync(Guid a, Guid b, CancellationToken ct = default) => Task.FromResult(false);
            public Task<bool> IsBlockedByAsync(Guid a, Guid b, CancellationToken ct = default) => Task.FromResult(false);
            public Task<List<Guid>> GetBlockedUserIdsAsync(Guid id, CancellationToken ct = default) => Task.FromResult(new List<Guid>());
            public Task<List<Guid>> GetRelatedUserIdsAsync(Guid id, CancellationToken ct = default) => Task.FromResult(new List<Guid>());
            public Task AddAsync(UserBlock block, CancellationToken ct = default) => Task.CompletedTask;
            public Task RemoveAsync(Guid a, Guid b, CancellationToken ct = default) => Task.CompletedTask;
        }

        private sealed class UnitOfWork : IUnitOfWork
        {
            public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
        }
    }
}
