using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;
using MindMatch.Infrastructure.Persistence;

namespace MindMatch.Infrastructure.Repositories;

public sealed class ReportRepository : IReportRepository
{
    private readonly MindMatchDbContext _context;
    public ReportRepository(MindMatchDbContext context) => _context = context;

    public Task AddAsync(UserReport report, CancellationToken cancellationToken = default) =>
        _context.UserReports.AddAsync(report, cancellationToken).AsTask();

    public Task<UserReport?> GetByIdAsync(Guid reportId, CancellationToken cancellationToken = default) =>
        _context.UserReports.Include(x => x.Messages).Include(x => x.Stories)
            .SingleOrDefaultAsync(x => x.Id == reportId, cancellationToken);

    public Task<List<UserReport>> GetByReporterAsync(Guid reporterId, CancellationToken cancellationToken = default) =>
        _context.UserReports.Where(x => x.ReporterId == reporterId)
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);

    public Task<List<UserReport>> GetQueueAsync(ReportStatus? status, int take, CancellationToken cancellationToken = default)
    {
        var query = _context.UserReports.AsQueryable();
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        return query.OrderBy(x => x.Status).ThenBy(x => x.CreatedAtUtc)
            .Take(take).ToListAsync(cancellationToken);
    }

    public Task AddAuditAsync(ModerationAudit audit, CancellationToken cancellationToken = default) =>
        _context.ModerationAudits.AddAsync(audit, cancellationToken).AsTask();

    public Task<List<ModerationAudit>> GetAuditByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.ModerationAudits.Where(x => x.TargetUserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);

    public async Task<int> ClearExpiredEvidenceBatchAsync(DateTime nowUtc, int take,
        CancellationToken cancellationToken = default)
    {
        var records = await _context.ReportedMessages.Where(x => x.EvidenceExpiresAtUtc <= nowUtc &&
                (x.MessageId != null || x.MediaAssetIdSnapshot != null || x.ContentSnapshot != null || x.MediaReferenceSnapshot != null))
            .OrderBy(x => x.EvidenceExpiresAtUtc).Take(take).ToListAsync(cancellationToken);
        var cleared = 0;
        foreach (var record in records)
            if (record.ClearExpiredEvidence(nowUtc)) cleared++;
        return cleared;
    }

    public async Task<int> ClearExpiredStoryEvidenceBatchAsync(DateTime nowUtc, int take,
        CancellationToken cancellationToken = default)
    {
        var records = await _context.ReportedStories.Where(x => x.EvidenceExpiresAtUtc <= nowUtc &&
                (x.StoryId != null || x.MediaAssetIdSnapshot != null || x.MediaReferenceSnapshot != null || x.CaptionSnapshot != null))
            .OrderBy(x => x.EvidenceExpiresAtUtc).Take(take).ToListAsync(cancellationToken);
        var cleared = 0;
        foreach (var record in records)
            if (record.ClearExpiredEvidence(nowUtc)) cleared++;
        return cleared;
    }
}
