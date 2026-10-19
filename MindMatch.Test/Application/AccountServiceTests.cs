using MindMatch.Application.Interfaces;
using MindMatch.Application.Services;
using MindMatch.Domain.Entities;

namespace MindMatch.Test.Application;

public sealed class AccountServiceTests
{
    [Fact]
    public async Task SelfFreeze_DeactivatesAccountAndInvalidatesExistingSessions()
    {
        var user = new User("user@example.com", "hash");
        var repository = new FakeUserRepository(user);
        var service = new AccountService(repository);

        await service.FreezeAsync(user.Id);

        Assert.False(user.IsActive);
        Assert.Equal(1, user.SessionVersion);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task OwnerCannotReactivateAdminFrozenAccount()
    {
        var user = new User("user@example.com", "hash");
        user.FreezeByAdmin(Guid.NewGuid(), "policy violation");
        var repository = new FakeUserRepository(user);
        var service = new AccountService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ActivateAsync(user.Id));

        Assert.False(user.IsActive);
        Assert.Equal(0, repository.SaveCount);
    }

    private sealed class FakeUserRepository(User user) : IUserRepository
    {
        public int SaveCount { get; private set; }
        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);
        public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(user.Id == userId ? user : null);
        public Task AddAsync(User item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
        public Task<List<User>> GetExpiredAdminFrozenBatchAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default) => Task.FromResult(new List<User>());
        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
