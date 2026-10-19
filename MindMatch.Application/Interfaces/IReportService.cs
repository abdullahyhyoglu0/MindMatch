using MindMatch.Application.Dtos.Moderation;
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Interfaces;

public interface IReportService
{
    Task<ReportResponse> SubmitAsync(Guid reporterId, CreateReportRequest request, CancellationToken cancellationToken = default);
    Task<List<ReportResponse>> GetMineAsync(Guid reporterId, CancellationToken cancellationToken = default);
    Task<List<ReportResponse>> GetQueueAsync(Guid actorId, ReportStatus? status, int take, CancellationToken cancellationToken = default);
    Task<ReportResponse> GetAdminDetailAsync(Guid actorId, Guid reportId, CancellationToken cancellationToken = default);
    Task AssignAsync(Guid actorId, Guid reportId, CancellationToken cancellationToken = default);
    Task ResolveAsync(Guid actorId, Guid reportId, string resolution, bool violationConfirmed, CancellationToken cancellationToken = default);
    Task<List<ModerationAudit>> GetModerationHistoryAsync(Guid actorId, Guid userId, CancellationToken cancellationToken = default);
    Task FreezeUserAsync(Guid actorId, Guid userId, string reason, DateTime? untilUtc, CancellationToken cancellationToken = default);
    Task ActivateUserAsync(Guid actorId, Guid userId, string reason, CancellationToken cancellationToken = default);
}
