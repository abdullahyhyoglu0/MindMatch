using MindMatch.Domain.Entities;

namespace MindMatch.Application.Interfaces;

public interface IPasswordResetTokenRepository
{
    Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken = default);
    Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task RevokeActiveForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<int> DeleteInactiveBatchAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default);
}
