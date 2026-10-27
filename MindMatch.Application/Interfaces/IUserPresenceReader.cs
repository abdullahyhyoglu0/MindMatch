namespace MindMatch.Application.Interfaces;

public interface IUserPresenceReader
{
    Task<(bool IsOnline, DateTime? LastSeenAtUtc)> GetStatusAsync(Guid userId);
    Task<IReadOnlyDictionary<Guid, (bool IsOnline, DateTime? LastSeenAtUtc)>> GetStatusesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);
}
