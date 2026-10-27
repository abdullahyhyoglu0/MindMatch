using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Services;

public sealed class UserBlockService : IUserBlockService
{
    private readonly IUserBlockRepository _repository;
    private readonly IUserRepository _users;
    private readonly IUserFollowRepository _follows;
    private readonly IUnitOfWork _unitOfWork;

    public UserBlockService(IUserBlockRepository repository, IUserRepository users,
        IUserFollowRepository follows, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _users = users;
        _follows = follows;
        _unitOfWork = unitOfWork;
    }

    public async Task BlockAsync(Guid blockerId, Guid blockedUserId, CancellationToken cancellationToken = default)
    {
        ValidateIds(blockerId, blockedUserId);
        if (await _users.GetByIdAsync(blockedUserId, cancellationToken) is null)
            throw new KeyNotFoundException("Kullanıcı bulunamadı.");
        if (!await _repository.IsBlockedByAsync(blockerId, blockedUserId, cancellationToken))
            await _repository.AddAsync(new UserBlock(blockerId, blockedUserId), cancellationToken);
        await _follows.RemoveBetweenAsync(blockerId, blockedUserId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UnblockAsync(Guid blockerId, Guid blockedUserId, CancellationToken cancellationToken = default)
    {
        ValidateIds(blockerId, blockedUserId);
        await _repository.RemoveAsync(blockerId, blockedUserId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task<List<Guid>> GetBlockedUsersAsync(Guid blockerId, CancellationToken cancellationToken = default)
    {
        if (blockerId == Guid.Empty) throw new ArgumentException("Kullanıcı kimliği geçersiz.");
        return _repository.GetBlockedUserIdsAsync(blockerId, cancellationToken);
    }

    private static void ValidateIds(Guid blockerId, Guid blockedUserId)
    {
        if (blockerId == Guid.Empty || blockedUserId == Guid.Empty)
            throw new ArgumentException("Kullanıcı kimlikleri gereklidir.");
        if (blockerId == blockedUserId)
            throw new ArgumentException("Kullanıcı kendisini engelleyemez.");
    }
}
