using MindMatch.Application.Dtos.Moderation;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Services;

public sealed class ReportService : IReportService
{
    private readonly IReportRepository _reports;
    private readonly IMessageRepository _messages;
    private readonly IConversationRepository _conversations;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;

    public ReportService(IReportRepository reports, IMessageRepository messages,
        IConversationRepository conversations, IUserRepository users, IUnitOfWork unitOfWork)
    {
        _reports = reports;
        _messages = messages;
        _conversations = conversations;
        _users = users;
        _unitOfWork = unitOfWork;
    }

    public async Task<ReportResponse> SubmitAsync(Guid reporterId, CreateReportRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (reporterId == Guid.Empty || request.ReportedUserId == Guid.Empty)
            throw new ArgumentException("Kullanıcı kimlikleri gereklidir.");
        var reporter = await _users.GetByIdAsync(reporterId, cancellationToken);
        if (reporter is null || !reporter.IsActive)
            throw new UnauthorizedAccessException("Etkin olmayan hesap rapor gönderemez.");
        if (await _users.GetByIdAsync(request.ReportedUserId, cancellationToken) is null)
            throw new KeyNotFoundException("Raporlanan kullanıcı bulunamadı.");
        if (request.MessageIds.Count > 10 || request.MessageIds.Distinct().Count() != request.MessageIds.Count)
            throw new ArgumentException("Bir rapora en fazla 10 farklı mesaj eklenebilir.");

        var report = new UserReport(reporterId, request.ReportedUserId, request.Reason,
            request.Description, request.ConversationId);

        if (request.ConversationId.HasValue)
        {
            var conversation = await _conversations.GetByIdAsync(request.ConversationId.Value, cancellationToken)
                ?? throw new KeyNotFoundException("Sohbet bulunamadı.");
            if (conversation.User1Id != reporterId && conversation.User2Id != reporterId)
                throw new UnauthorizedAccessException("Bu sohbeti raporlamak için yetkiniz yok.");
            if (conversation.User1Id != request.ReportedUserId && conversation.User2Id != request.ReportedUserId)
                throw new ArgumentException("Raporlanan kullanıcı seçilen sohbette bulunmuyor.");
        }
        else if (request.MessageIds.Count > 0)
            throw new ArgumentException("Mesaj kanıtı için sohbet kimliği gereklidir.");

        if (request.MessageIds.Count > 0)
        {
            var selectedMessages = await _messages.GetByIdsAsync(request.MessageIds, cancellationToken);
            if (selectedMessages.Count != request.MessageIds.Count || selectedMessages.Any(x =>
                x.ConversationId != request.ConversationId || x.SenderId != request.ReportedUserId))
                throw new ArgumentException("Yalnızca raporlanan kullanıcının seçili sohbetteki mesajları kanıt olarak eklenebilir.");

            foreach (var message in selectedMessages)
                report.AddMessageSnapshot(new ReportedMessage(report.Id, message));
        }

        await _reports.AddAsync(report, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(report, includeMessages: false);
    }

    public async Task<List<ReportResponse>> GetMineAsync(Guid reporterId, CancellationToken cancellationToken = default)
    {
        if (reporterId == Guid.Empty) throw new ArgumentException("Kullanıcı kimliği geçersiz.");
        return (await _reports.GetByReporterAsync(reporterId, cancellationToken))
            .Select(x => Map(x, false)).ToList();
    }

    public async Task<List<ReportResponse>> GetQueueAsync(Guid actorId, ReportStatus? status, int take, CancellationToken cancellationToken = default)
    {
        await EnsureModerator(actorId, cancellationToken);
        if (take is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(take));
        return (await _reports.GetQueueAsync(status, take, cancellationToken))
            .Select(x => Map(x, false)).ToList();
    }

    public async Task<ReportResponse> GetAdminDetailAsync(Guid actorId, Guid reportId, CancellationToken cancellationToken = default)
    {
        await EnsureModerator(actorId, cancellationToken);
        var report = await GetReport(reportId, cancellationToken);
        var response = Map(report, true);
        await _reports.AddAuditAsync(new ModerationAudit(actorId, ModerationActionType.ReportViewed,
            "Rapor ayrıntısı görüntülendi.", report.ReportedUserId, report.Id), cancellationToken);
        if (response.Messages.Count > 0)
            await _reports.AddAuditAsync(new ModerationAudit(actorId,
                ModerationActionType.ReportEvidenceViewed,
                $"Rapor mesaj kanıtı görüntülendi ({response.Messages.Count} kayıt).",
                report.ReportedUserId, report.Id), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task AssignAsync(Guid actorId, Guid reportId, CancellationToken cancellationToken = default)
    {
        await EnsureModerator(actorId, cancellationToken);
        var report = await GetReport(reportId, cancellationToken);
        report.Assign(actorId);
        await _reports.AddAuditAsync(new ModerationAudit(actorId, ModerationActionType.ReportAssigned,
            "Rapor moderatöre atandı.", report.ReportedUserId, report.Id), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ResolveAsync(Guid actorId, Guid reportId, string resolution, bool violationConfirmed, CancellationToken cancellationToken = default)
    {
        await EnsureModerator(actorId, cancellationToken);
        var report = await GetReport(reportId, cancellationToken);
        report.Resolve(actorId, resolution, violationConfirmed);
        await _reports.AddAuditAsync(new ModerationAudit(actorId, ModerationActionType.ReportResolved,
            resolution, report.ReportedUserId, report.Id), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ModerationAudit>> GetModerationHistoryAsync(Guid actorId, Guid userId, CancellationToken cancellationToken = default)
    {
        await EnsureModerator(actorId, cancellationToken);
        return await _reports.GetAuditByUserAsync(userId, cancellationToken);
    }

    public async Task FreezeUserAsync(Guid actorId, Guid userId, string reason, DateTime? untilUtc, CancellationToken cancellationToken = default)
    {
        var actor = await GetUser(actorId, cancellationToken);
        if (actor.Role != UserRole.Admin) throw new UnauthorizedAccessException("Hesap dondurma yalnızca yönetici yetkisi gerektirir.");
        if (actorId == userId) throw new InvalidOperationException("Yönetici kendi hesabını bu akıştan donduramaz.");
        var target = await GetUser(userId, cancellationToken);
        if (target.Role == UserRole.Admin) throw new UnauthorizedAccessException("Bu yönetici hesabını bu moderasyon akışı üzerinden yönetemezsiniz.");
        target.FreezeByAdmin(actorId, reason, untilUtc);
        await _reports.AddAuditAsync(new ModerationAudit(actorId, ModerationActionType.UserFrozen, reason, userId), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ActivateUserAsync(Guid actorId, Guid userId, string reason, CancellationToken cancellationToken = default)
    {
        var actor = await GetUser(actorId, cancellationToken);
        if (actor.Role != UserRole.Admin) throw new UnauthorizedAccessException("Hesabı aktifleştirmek için yönetici yetkisi gerekir.");
        if (actorId == userId) throw new InvalidOperationException("Yönetici kendi hesabını bu akıştan aktifleştiremez.");
        var target = await GetUser(userId, cancellationToken);
        if (target.Role == UserRole.Admin) throw new UnauthorizedAccessException("Bu yönetici hesabını bu moderasyon akışı üzerinden yönetemezsiniz.");
        target.ActivateByAdmin();
        await _reports.AddAuditAsync(new ModerationAudit(actorId, ModerationActionType.UserActivated, reason, userId), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureModerator(Guid actorId, CancellationToken cancellationToken)
    {
        var actor = await GetUser(actorId, cancellationToken);
        if (actor.Role is not (UserRole.Admin or UserRole.Moderator))
            throw new UnauthorizedAccessException("Moderatör yetkisi gereklidir.");
    }

    private async Task<User> GetUser(Guid id, CancellationToken cancellationToken) =>
        await _users.GetByIdAsync(id, cancellationToken) ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");

    private async Task<UserReport> GetReport(Guid id, CancellationToken cancellationToken) =>
        await _reports.GetByIdAsync(id, cancellationToken) ?? throw new KeyNotFoundException("Rapor bulunamadı.");

    private static ReportResponse Map(UserReport x, bool includeMessages)
    {
        var now = DateTime.UtcNow;
        return new ReportResponse
        {
            Id = x.Id, ReporterId = x.ReporterId, ReportedUserId = x.ReportedUserId,
            ConversationId = x.ConversationId, Reason = x.Reason, Description = x.Description,
            Status = x.Status, AssignedModeratorId = x.AssignedModeratorId,
            Resolution = x.Resolution, CreatedAtUtc = x.CreatedAtUtc, ResolvedAtUtc = x.ResolvedAtUtc,
            Messages = includeMessages ? x.Messages.Where(m => m.EvidenceExpiresAtUtc > now)
                .Select(m => new ReportedMessageResponse
                {
                    MessageId = m.MessageId, SenderId = m.SenderId,
                    MediaAssetIdSnapshot = m.MediaAssetIdSnapshot,
                    ContentSnapshot = m.ContentSnapshot, MediaReferenceSnapshot = m.MediaReferenceSnapshot,
                    MessageType = m.MessageType, MessageCreatedAtUtc = m.MessageCreatedAtUtc
                }).ToList() : []
        };
    }
}
