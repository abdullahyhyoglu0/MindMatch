namespace MindMatch.Application.Interfaces;

public interface IUserBlockService
{
    Task BlockAsync(Guid blockerId, Guid blockedUserId, CancellationToken cancellationToken = default);
    Task UnblockAsync(Guid blockerId, Guid blockedUserId, CancellationToken cancellationToken = default);
    Task<List<Guid>> GetBlockedUsersAsync(Guid blockerId, CancellationToken cancellationToken = default);
}
