namespace MindMatch.Application.Interfaces;

public interface IAccountService
{
    Task<AccountStatusResponse> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default);
    Task FreezeAsync(Guid userId, CancellationToken cancellationToken = default);
    Task ActivateAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class AccountStatusResponse
{
    public bool IsActive { get; init; }
    public bool IsFrozenByAdmin { get; init; }
    public DateTime? SelfDeactivatedAtUtc { get; init; }
    public string? AdminFreezeReason { get; init; }
    public DateTime? AdminFrozenUntilUtc { get; init; }
}
