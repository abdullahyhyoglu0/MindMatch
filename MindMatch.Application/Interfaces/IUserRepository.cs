
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);



    Task AddAsync(
        User user,
        CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<List<User>> GetExpiredAdminFrozenBatchAsync(DateTime nowUtc, int take,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
