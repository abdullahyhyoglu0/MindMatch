using MindMatch.Domain.Entities;

namespace MindMatch.Application.Interfaces;

public interface IReportRepository
{
    Task AddAsync(UserReport report, CancellationToken cancellationToken = default);
    Task<UserReport?> GetByIdAsync(Guid reportId, CancellationToken cancellationToken = default);
    Task<List<UserReport>> GetByReporterAsync(Guid reporterId, CancellationToken cancellationToken = default);
    Task<List<UserReport>> GetQueueAsync(ReportStatus? status, int take, CancellationToken cancellationToken = default);
    Task AddAuditAsync(ModerationAudit audit, CancellationToken cancellationToken = default);
    Task<List<ModerationAudit>> GetAuditByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<int> ClearExpiredEvidenceBatchAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default);
    Task<int> ClearExpiredStoryEvidenceBatchAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default);
}
