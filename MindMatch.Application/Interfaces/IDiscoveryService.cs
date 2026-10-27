using MindMatch.Application.Dtos.UserProfile;

namespace MindMatch.Application.Interfaces;

public interface IDiscoveryService
{
    Task<DiscoveryPageResponse> DiscoverAsync(Guid userId, int page, int pageSize, string? city,
        bool premiumOnly, CancellationToken cancellationToken = default);
    Task RecordDecisionAsync(Guid userId, Guid targetUserId, bool rejected,
        CancellationToken cancellationToken = default);
    Task<DiscoverPeoplePageResponse> ListPeopleAsync(Guid userId, int page, int pageSize,
        CancellationToken cancellationToken = default);
}
