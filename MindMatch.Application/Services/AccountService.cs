using MindMatch.Application.Interfaces;

namespace MindMatch.Application.Services;

public sealed class AccountService : IAccountService
{
    private readonly IUserRepository _users;

    public AccountService(IUserRepository users) => _users = users;

    public async Task<AccountStatusResponse> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUser(userId, cancellationToken);
        return new AccountStatusResponse
        {
            IsActive = user.IsActive,
            IsFrozenByAdmin = user.IsFrozenByAdmin,
            SelfDeactivatedAtUtc = user.SelfDeactivatedAtUtc,
            AdminFreezeReason = user.IsFrozenByAdmin ? user.AdminFreezeReason : null,
            AdminFrozenUntilUtc = user.AdminFrozenUntilUtc
        };
    }

    public async Task FreezeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUser(userId, cancellationToken);
        user.Deactivate();
        await _users.SaveChangesAsync(cancellationToken);
    }

    public async Task ActivateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUser(userId, cancellationToken);
        user.Activate();
        await _users.SaveChangesAsync(cancellationToken);
    }

    private async Task<Domain.Entities.User> GetUser(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty) throw new ArgumentException("Kullanıcı kimliği geçersiz.");
        return await _users.GetByIdAsync(userId, cancellationToken)
            ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");
    }
}
